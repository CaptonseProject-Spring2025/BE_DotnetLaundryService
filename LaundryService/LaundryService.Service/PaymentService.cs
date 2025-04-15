using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Domain.Interfaces;
using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LaundryService.Dto.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Net.payOS.Types;
using Net.payOS;

namespace LaundryService.Service
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUtil _util;
        private readonly IConfiguration _configuration;

        public PaymentService(IUnitOfWork unitOfWork, IUtil util, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _util = util;
            _configuration = configuration;
        }

        public async Task<PaymentMethodResponse> CreatePaymentMethodAsync(CreatePaymentMethodRequest request)
        {
            // 1) Kiểm tra trùng tên
            var repo = _unitOfWork.Repository<Paymentmethod>();
            var existing = await repo.GetAll()
                .FirstOrDefaultAsync(pm => pm.Name == request.Name);

            if (existing != null)
            {
                throw new ApplicationException($"PaymentMethod '{request.Name}' đã tồn tại.");
            }

            // 2) Tạo entity mới
            var newPaymentMethod = new Paymentmethod
            {
                Paymentmethodid = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Isactive = request.IsActive ?? true,
                Createdat = DateTime.UtcNow
            };

            // 3) Lưu vào DB
            await _unitOfWork.BeginTransaction();
            try
            {
                await repo.InsertAsync(newPaymentMethod, saveChanges: false);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }

            // 4) Map sang DTO trả về
            var response = new PaymentMethodResponse
            {
                PaymentMethodId = newPaymentMethod.Paymentmethodid,
                Name = newPaymentMethod.Name,
                Description = newPaymentMethod.Description,
                IsActive = newPaymentMethod.Isactive ?? true,
                CreatedAt = _util.ConvertToVnTime(newPaymentMethod.Createdat ?? DateTime.UtcNow)
            };

            return response;
        }

        public async Task<List<PaymentMethodResponse>> GetAllPaymentMethodsAsync()
        {
            // 1) Lấy toàn bộ PaymentMethod từ DB
            var repo = _unitOfWork.Repository<Paymentmethod>();
            var allMethods = await repo
                .GetAll()
                .OrderBy(pm => pm.Createdat)
                .ToListAsync();

            // 2) Map sang DTO
            var result = allMethods.Select(pm => new PaymentMethodResponse
            {
                PaymentMethodId = pm.Paymentmethodid,
                Name = pm.Name,
                Description = pm.Description,
                IsActive = pm.Isactive ?? true,
                CreatedAt = _util.ConvertToVnTime(pm.Createdat ?? DateTime.UtcNow)
            }).ToList();

            return result;
        }

        /// <summary>
        /// Tạo link thanh toán PayOS
        /// </summary>
        public async Task<CreatePayOSPaymentLinkResponse> CreatePayOSPaymentLinkAsync(CreatePayOSPaymentLinkRequest request)
        {
            // 1) Validate Order => lấy Order + User
            var order = _unitOfWork.Repository<Order>()
                .GetAll()
                .Include(o => o.User)
                .FirstOrDefault(o => o.Orderid == request.OrderId);

            if (order == null)
                throw new KeyNotFoundException($"Không tìm thấy Order: {request.OrderId}");

            if (order.Totalprice == null || order.Totalprice <= 0)
                throw new ApplicationException("Order chưa có giá trị totalPrice hợp lệ.");

            // Lấy user => buyerName, buyerPhone
            var user = order.User;
            if (user == null)
                throw new KeyNotFoundException($"Không tìm thấy userId = {order.Userid}");

            // 2) Lấy PaymentMethod "PayOS" => ID
            var payosMethod = _unitOfWork.Repository<Paymentmethod>()
                .GetAll()
                .FirstOrDefault(pm => pm.Name == "PayOS"); // Tên method tuỳ logic
            if (payosMethod == null)
                throw new ApplicationException("Chưa cài đặt PaymentMethod: 'PayOS' trong bảng PaymentMethods.");

            // 3) Tạo PayOS client
            var clientId = _configuration["PayOS:ClientID"];
            var apiKey = _configuration["PayOS:APIKey"];
            var checksumKey = _configuration["PayOS:ChecksumKey"];

            var payOS = new PayOS(clientId, apiKey, checksumKey);

            // Tạo request body gửi sang PayOS
            long orderCodeLong = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            //    - amount = (int)order.Totalprice
            var amountInt = (int)order.Totalprice.Value;

            //    - buyerName = user.Fullname
            var buyerName = user.Fullname ?? "Unknown";

            //    - buyerPhone = user.Phonenumber ?? "NoPhone"
            var buyerPhone = user.Phonenumber ?? "NoPhone";

            var cancelUrl = _configuration["PayOS:CancelUrl"];
            var returnUrl = _configuration["PayOS:ReturnUrl"];

            //    - expiredAt = 15p sau => to UnixTime
            var expiredAt = DateTimeOffset.Now.AddMinutes(Convert.ToDouble(_configuration["PayOS:ExpiryInMinutes"])).ToUnixTimeSeconds();

            //  ignature = SHA256( orderId + checksumKey )
            var rawSign = request.OrderId + checksumKey;
            var signature = CalculateSha256(rawSign);

            // Tạo 1 list items (tuỳ, demo 1 item)
            var items = new List<ItemData>
            {
                new ItemData("Payment for OrderId=" + order.Orderid, 1, amountInt)
            };

            // Tạo PaymentData (nếu dùng SDK payOS)
            var paymentData = new PaymentData(
                orderCodeLong,
                amountInt,
                request.Description.Trim(),
                items,
                cancelUrl,
                returnUrl
)
            {
                expiredAt = (int)expiredAt,
                buyerName = buyerName,
                buyerPhone = buyerPhone,
                signature = signature
            };            

            CreatePaymentResult payosResponse;
            try
            {
                payosResponse = await payOS.createPaymentLink(paymentData);
            }
            catch (Exception ex)
            {
                // Bắt lỗi call SDK 
                throw new ApplicationException($"Lỗi khi gọi PayOS: {ex.Message}", ex);
            }

            // Kiểm tra code trả về?
            // if (payosResponse.code != "00") throw new ApplicationException("PayOS error: ...");

            // 5) Lưu Payment record
            await _unitOfWork.BeginTransaction();
            try
            {
                // Tạo Payment entity
                var payment = new Payment
                {
                    Paymentid = Guid.NewGuid(),
                    Orderid = order.Orderid,
                    Paymentdate = DateTime.UtcNow,
                    Amount = order.Totalprice.Value,
                    Paymentmethodid = payosMethod.Paymentmethodid, // method PayOS
                    Paymentstatus = payosResponse.status,          // data.status 
                    Transactionid = payosResponse.paymentLinkId,   // data.paymentLinkId
                    Paymentmetadata = System.Text.Json.JsonSerializer.Serialize(payosResponse), // lưu toàn bộ data 
                    Createdat = DateTime.UtcNow
                };

                await _unitOfWork.Repository<Payment>().InsertAsync(payment, saveChanges: false);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }

            // 6) Chuẩn bị response
            //    - data.checkoutUrl => payosResponse.checkoutUrl
            //    - data.qrCode => payosResponse.qrCode
            //    - data.paymentLinkId => payosResponse.paymentLinkId
            //    - data.status => payosResponse.status
            var resp = new CreatePayOSPaymentLinkResponse
            {
                CheckoutUrl = payosResponse.checkoutUrl,
                QrCode = payosResponse.qrCode,
                PaymentLinkId = payosResponse.paymentLinkId,
                Status = payosResponse.status
            };

            return resp;
        }

        private string CalculateSha256(string rawSign)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(rawSign);
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}
