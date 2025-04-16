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
using System.Text.Json;
using Amazon.Runtime.Internal.Util;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace LaundryService.Service
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUtil _util;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(IUnitOfWork unitOfWork, IUtil util, IConfiguration configuration, ILogger<PaymentService> logger)
        {
            _unitOfWork = unitOfWork;
            _util = util;
            _configuration = configuration;
            _logger = logger;
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
                    .Include(o => o.Orderitems)
                        .ThenInclude(oi => oi.Orderextras)
                            .ThenInclude(e => e.Extra)
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Service)
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

            var cancelUrl = _configuration["PayOS:CancelUrl"];
            var returnUrl = _configuration["PayOS:ReturnUrl"];

            //    - expiredAt = 15p sau => to UnixTime
            var expiredAt = DateTimeOffset.Now.AddMinutes(Convert.ToDouble(_configuration["PayOS:ExpiryInMinutes"])).ToUnixTimeSeconds();

            //  ignature = SHA256( orderId + checksumKey )
            var rawSign = request.OrderId + checksumKey;
            var signature = CalculateSha256(rawSign);

            // Tạo danh sách items từ OrderItems
            var itemDataList = new List<ItemData>();
            foreach (var oi in order.Orderitems)
            {
                // service price
                var serviceName = oi.Service?.Name ?? "Unknown Service";
                decimal servicePrice = oi.Service?.Price ?? 0;
                // tính tổng extras
                decimal extrasSum = 0;
                var extraNames = new List<string>();
                foreach (var oe in oi.Orderextras)
                {
                    var exName = oe.Extra?.Name ?? "Extra";
                    var exPrice = oe.Extra?.Price ?? 0;
                    extrasSum += exPrice;
                    extraNames.Add(exName);
                }

                // Tên item: ghép service + tên extras
                var combinedName = serviceName;
                if (extraNames.Any())
                {
                    combinedName += " + " + string.Join(", ", extraNames);
                }

                // Price = servicePrice + extrasSum
                // Lưu ý: PayOS chờ kiểu int. Nếu sợ vượt int, xem có logic cap or throw?
                var finalUnitPrice = (int)(servicePrice + extrasSum);
                // Số lượng
                var quantity = oi.Quantity;

                itemDataList.Add(new ItemData(
                    combinedName,
                    quantity,
                    finalUnitPrice
                ));
            }

            // Xử lý request.Description: nếu rỗng => dùng orderId
            var desc = string.IsNullOrWhiteSpace(request.Description)
                ? request.OrderId
                : request.Description.Trim();

            // Tạo PaymentData (nếu dùng SDK payOS)
            var paymentData = new PaymentData(
                orderCodeLong,
                amountInt,
                desc,
                itemDataList,
                cancelUrl,
                returnUrl
)
            {
                expiredAt = (int)expiredAt,
                buyerName = user.Fullname ?? "Unknown",
                buyerPhone = user.Phonenumber ?? "NoPhone",
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

            // Paymentid để lưu vào DB
            var paymentId = Guid.NewGuid();

            // Lưu Payment record
            await _unitOfWork.BeginTransaction();
            try
            {
                // Tạo Payment entity
                var payment = new Payment
                {
                    Paymentid = paymentId,
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
                PaymentId = paymentId,
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

        /// <summary>
        /// Lấy thông tin link thanh toán PayOS dựa vào paymentId
        /// </summary>
        public async Task<PaymentLinkInfoResponse> GetPayOSPaymentLinkInfoAsync(Guid paymentId)
        {
            // 1) Tìm Payment => parse paymentmetadata để lấy orderCode (long)
            var payment = _unitOfWork.Repository<Payment>()
                .GetAll()
                .FirstOrDefault(p => p.Paymentid == paymentId);

            if (payment == null)
                throw new KeyNotFoundException($"Không tìm thấy PaymentId={paymentId}.");

            if (string.IsNullOrWhiteSpace(payment.Paymentmetadata))
                throw new ApplicationException("Paymentmetadata rỗng, không có orderCode PayOS.");

            // Giả sử paymentmetadata JSON có field "orderCode" => parse
            // Demo parse object, bám theo data sample
            var pmJsonDoc = JsonDocument.Parse(payment.Paymentmetadata);
            var rootEl = pmJsonDoc.RootElement;
            if (!rootEl.TryGetProperty("orderCode", out var orderCodeEl))
                throw new ApplicationException("Không tìm thấy 'orderCode' trong paymentmetadata.");

            var orderCodeLong = orderCodeEl.GetInt64();

            // 2) Gọi PayOS => getPaymentLinkInformation(orderCodeLong)
            var clientId = _configuration["PayOS:ClientID"];
            var apiKey = _configuration["PayOS:APIKey"];
            var checksumKey = _configuration["PayOS:ChecksumKey"];
            var payOS = new PayOS(clientId, apiKey, checksumKey);

            PaymentLinkInformation payosInfo;
            try
            {
                payosInfo = await payOS.getPaymentLinkInformation(orderCodeLong);
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Lỗi khi gọi payOS.getPaymentLinkInformation: {ex.Message}", ex);
            }

            if (payosInfo == null)
            {
                // code != "00" hoặc data=null => có thể quăng lỗi
                throw new ApplicationException(
                    $"Không lấy được data PaymentLinkInformation từ PayOS. code={payosInfo}"
                );
            }

            // 3) Convert payosInfo.data => PaymentLinkInfoResponse
            //    createdAt, canceledAt => convert sang giờ VN (nếu parse OK)
            DateTime createdAtVn = DateTime.UtcNow;
            if (DateTime.TryParse(payosInfo.createdAt, out var dt))
            {
                // PayOS trả "2023-08-01T19:44:15.000Z" => Z => UTC
                dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

                createdAtVn = _util.ConvertToVnTime(dt);
            }

            DateTime? canceledAtVn = null;
            if (!string.IsNullOrWhiteSpace(payosInfo.canceledAt))
            {
                if (DateTime.TryParse(payosInfo.canceledAt, out var cat))
                {
                    canceledAtVn = _util.ConvertToVnTime(cat);
                }
            }

            var response = new PaymentLinkInfoResponse
            {
                Id = payosInfo.id ?? "",
                OrderCode = payosInfo.orderCode,
                Amount = payosInfo.amount,
                AmountPaid = payosInfo.amountPaid,
                AmountRemaining = payosInfo.amountRemaining,
                Status = payosInfo.status ?? "",
                CreatedAt = createdAtVn,
                CancellationReason = payosInfo.cancellationReason,
                CanceledAt = canceledAtVn,
            };

            // map transactions => PaymentLinkTransactionResponse
            if (payosInfo.transactions != null)
            {
                foreach (var t in payosInfo.transactions)
                {
                    DateTime transDateTimeVn = DateTime.UtcNow;
                    if (DateTime.TryParse(t.transactionDateTime, out var dtT))
                    {
                        // PayOS trả "2023-08-01T19:44:15.000Z" => Z => UTC
                        dtT = DateTime.SpecifyKind(dtT, DateTimeKind.Utc);

                        transDateTimeVn = _util.ConvertToVnTime(dtT);
                    }

                    response.Transactions.Add(new PaymentLinkTransactionResponse
                    {
                        Reference = t.reference ?? "",
                        Amount = t.amount,
                        AccountNumber = t.accountNumber,
                        Description = t.description,
                        TransactionDateTime = transDateTimeVn,
                        VirtualAccountName = t.virtualAccountName,
                        VirtualAccountNumber = t.virtualAccountNumber,
                        CounterAccountBankId = t.counterAccountBankId,
                        CounterAccountBankName = t.counterAccountBankName,
                        CounterAccountName = t.counterAccountName,
                        CounterAccountNumber = t.counterAccountNumber
                    });
                }
            }

            return response;
        }

        /// <summary>
        /// Xử lý callback sau khi PayOS redirect
        /// </summary>
        public async Task<string> ConfirmPayOSCallbackAsync(string paymentLinkId, string status)
        {
            // 1) Bắt đầu transaction
            await _unitOfWork.BeginTransaction();

            try
            {
                // 2) Tìm Payment => transactionid = paymentLinkId
                var payment = _unitOfWork.Repository<Payment>()
                    .GetAll()
                    .FirstOrDefault(p => p.Transactionid == paymentLinkId);

                if (payment == null)
                    throw new KeyNotFoundException($"Payment associated with PayOS link ID '{paymentLinkId}' not found.");

                // 3) Cập nhật Paymentstatus
                payment.Paymentstatus = status;
                payment.Updatedat = DateTime.UtcNow;
                await _unitOfWork.Repository<Payment>().UpdateAsync(payment, saveChanges: false);

                // 4) Lưu DB + commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();

                // 5) return url redirect về frontend
                var thankYouUrl = _configuration["PayOS:ThankYouUrl"];
                if (string.IsNullOrEmpty(thankYouUrl))
                {
                    // fallback
                    thankYouUrl = "captonse-project-fe.vercel.app";
                }

                // Tuỳ ý gắn param => "?paymentId=..."
                //var finalLink = $"{thankYouUrl}?paymentId={payment.Paymentid}&status={status}";
                //return finalLink;
                return thankYouUrl;
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        // --- Phương thức xử lý Webhook ---
        public async Task HandlePayOSWebhookAsync(WebhookType webhookBody)
        {
            _logger.LogInformation("Received PayOS webhook.");

            // 1) Tạo PayOS client
            var clientId = _configuration["PayOS:ClientID"];
            var apiKey = _configuration["PayOS:APIKey"];
            var checksumKey = _configuration["PayOS:ChecksumKey"];
            var payOS = new PayOS(clientId, apiKey, checksumKey);

            // 2) Xác minh chữ ký => verifyPaymentWebhookData
            WebhookData verifiedData;
            try
            {
                verifiedData = payOS.verifyPaymentWebhookData(webhookBody);
                _logger.LogInformation("Webhook signature verified successfully for OrderCode: {OrderCode}", verifiedData.orderCode);
            }
            catch (Exception ex)
            {
                // Nếu xác minh chữ ký sai => ném lỗi
                _logger.LogError(ex, "PayOS webhook signature verification failed.");
                throw new ApplicationException($"Webhook signature invalid: {ex.Message}", ex);
            }

            if (verifiedData == null)
            {
                _logger.LogError("PayOS verifyPaymentWebhookData returned null after signature check (unexpected).");
                throw new ApplicationException("Webhook verification returned invalid data.");
            }

            // 3) Kiểm tra code= "00", success= true => success
            //    code != "00" => fail / partial
            //    Tùy logic => Paymentstatus
            var code = verifiedData.code; // "00" or "99"...
            var desc = verifiedData.desc; // "success" ...
            var orderCode = verifiedData.orderCode; // long
            var paymentLinkId = verifiedData.paymentLinkId;

            // 4) Bắt đầu transaction
            await _unitOfWork.BeginTransaction();
            try
            {
                // 4.1) Tìm Payment => gợi ý: 
                //      - Tìm theo transactionid= paymentLinkId, 
                //        hoặc parse Paymentmetadata -> so sánh orderCode
                var paymentRepo = _unitOfWork.Repository<Payment>();

                var payment = paymentRepo
                    .GetAll()
                    .FirstOrDefault(p => p.Transactionid == paymentLinkId);

                if (payment == null)
                {
                    // Nếu transactionid ko thấy => ta parse Paymentmetadata so sánh orderCode
                    var allPayments = paymentRepo.GetAll().ToList();
                    payment = allPayments.FirstOrDefault(p =>
                    {
                        if (string.IsNullOrWhiteSpace(p.Paymentmetadata))
                            return false;

                        try
                        {
                            using var doc = JsonDocument.Parse(p.Paymentmetadata);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("orderCode", out var ocEl))
                            {
                                long oc = ocEl.GetInt64();
                                return oc == orderCode;
                            }
                        }
                        catch { /* ignore */ }
                        return false;
                    });
                }

                if (payment == null)
                {
                    _logger.LogError("Payment not found for PaymentLinkId: {PaymentLinkId} and OrderCode: {OrderCode}", paymentLinkId, orderCode);
                    throw new KeyNotFoundException($"Payment with linkId={paymentLinkId} or orderCode={orderCode} not found");
                }

                // biến lưu trạng thái cuối cùng
                string finalStatus;
                //      code= "00" & success= true => "PAID" 
                //      code= "99" => "FAILED"? 
                if (code == "00" && webhookBody.success == true)
                {
                    // success
                    finalStatus = "PAID";
                }
                else
                {
                    finalStatus = "FAILED";
                }
                _logger.LogInformation("Webhook indicates final status: {Status} for PaymentLinkId: {PaymentLinkId}", finalStatus, verifiedData.paymentLinkId);

                // --- 4.2) Cập nhật Paymentstatus ----
                // Idempotency Check: Nếu trạng thái trong DB đã là trạng thái cuối cùng rồi thì bỏ qua
                // Điều này quan trọng vì webhook có thể được gửi lại nhiều lần.
                if (payment.Paymentstatus == finalStatus)
                {
                    _logger.LogInformation("Payment status for Payment {PaymentId} is already '{Status}'. Webhook is likely a duplicate. Skipping update.", payment.Paymentid, finalStatus);
                    return; // Bỏ qua nếu đã ở trạng thái cuối cùng
                }
                if (payment.Paymentstatus == "PAID" && finalStatus != "PAID")
                {
                    // Cẩn thận: Nếu đã PAID rồi mà webhook báo trạng thái khác -> có vấn đề, cần điều tra.
                    _logger.LogWarning("Payment {PaymentId} is already PAID, but received webhook with status '{Status}'. Potential issue. Skipping update.", payment.Paymentid, finalStatus);
                    return; // Không nên thay đổi trạng thái từ PAID thành trạng thái khác qua webhook tự động.
                }

                DateTime transDateTimeVn = DateTime.UtcNow;

                if (DateTime.TryParse(verifiedData.transactionDateTime, out var dtT))
                {
                    // Không convert giờ nữa, chỉ cần đánh dấu là UTC để PostgreSQL cho phép lưu
                    transDateTimeVn = DateTime.SpecifyKind(dtT, DateTimeKind.Utc);
                }

                // Cập nhật Payment
                payment.Paymentstatus = finalStatus;
                payment.Updatedat = DateTime.UtcNow;
                payment.Paymentdate = transDateTimeVn;

                await paymentRepo.UpdateAsync(payment, saveChanges: false);

                // 4.3) Lưu + commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
                _logger.LogInformation("Successfully processed webhook and committed database changes for Payment {PaymentId}.", payment.Paymentid);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransaction();

                // Throw lỗi để Controller biết xử lý thất bại và trả về lỗi 500 cho PayOS (PayOS có thể thử gửi lại webhook)
                throw new ApplicationException($"Failed to update database after processing webhook for payment link ID '{verifiedData.paymentLinkId}'.", ex);
            }
        }
    }
}
