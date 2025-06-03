using DocumentFormat.OpenXml.Office2016.Drawing.Charts;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Pagination;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using LaundryService.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUtil _util;
        private readonly IMapboxService _mapboxService;
        private readonly ILogger<OrderService> _logger;
        private readonly IOrderJobService _jobService;

        public OrderService(IUnitOfWork unitOfWork, IUtil util, IMapboxService mapboxService, ILogger<OrderService> logger, IOrderJobService jobService)
        {
            _unitOfWork = unitOfWork;
            _util = util;
            _mapboxService = mapboxService;
            _logger = logger;
            _jobService = jobService;
        }

        /// <summary>
        /// Hàm private thêm sản phẩm vào giỏ hàng mà không cần transaction.
        /// </summary>
        public async Task AddToCartNoTransactionAsync(Guid userId, AddToCartRequest request)
        {
            // 1) Kiểm tra ServiceDetail
            var serviceDetail = await _unitOfWork
                .Repository<Servicedetail>()
                .GetAsync(s => s.Serviceid == request.ServiceDetailId);

            if (serviceDetail == null)
                throw new KeyNotFoundException("Service detail not found.");

            // 2) Tìm Order INCART
            var order = _unitOfWork.Repository<Order>()
                .GetAll()
                .FirstOrDefault(o => o.Userid == userId && o.Currentstatus == "INCART");

            if (order == null)
            {
                // Chưa có => tạo mới
                order = new Order
                {
                    Orderid = _util.GenerateOrderId(),
                    Userid = userId,
                    Currentstatus = "INCART",
                    Createdat = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Order>().InsertAsync(order);
                await _unitOfWork.SaveChangesAsync();
            }

            // 3) Kiểm tra ExtraIds
            var validExtras = new List<Extra>();
            if (request.ExtraIds != null && request.ExtraIds.Count > 0)
            {
                validExtras = _unitOfWork.Repository<Extra>()
                    .GetAll()
                    .Where(e => request.ExtraIds.Contains(e.Extraid))
                    .ToList();

                var invalidIds = request.ExtraIds.Except(validExtras.Select(x => x.Extraid)).ToList();
                if (invalidIds.Any())
                {
                    throw new ApplicationException($"Some extras not found: {string.Join(", ", invalidIds)}");
                }
            }

            // 4) Tìm xem đã có OrderItem trùng ServiceDetail & EXACT extras chưa
            //    (Sẽ load kèm OrderExtras để so sánh)
            //    Chỉ load những OrderItem có ServiceID == serviceDetail.Serviceid
            //    Rồi so sánh ExtraIds
            var orderItemsSameService = _unitOfWork.Repository<Orderitem>()
                .GetAll()
                .Where(oi => oi.Orderid == order.Orderid && oi.Serviceid == serviceDetail.Serviceid)
                .ToList();

            Orderitem matchedItem = null;
            foreach (var oi in orderItemsSameService)
            {
                // Lấy list OrderExtra gắn với OrderItem này
                var oiExtras = _unitOfWork.Repository<Orderextra>()
                    .GetAll()
                    .Where(e => e.Orderitemid == oi.Orderitemid)
                    .ToList();

                // So sánh
                if (ExtrasAreTheSame(oiExtras, request.ExtraIds ?? new List<Guid>()))
                {
                    matchedItem = oi;
                    break;
                }
            }

            // 5) Nếu matchedItem != null => tăng Quantity
            if (matchedItem != null)
            {
                matchedItem.Quantity += request.Quantity;
                await _unitOfWork.Repository<Orderitem>().UpdateAsync(matchedItem, saveChanges: false);
            }
            else
            {
                // Ngược lại => tạo OrderItem mới
                var newOrderItem = new Orderitem
                {
                    Orderid = order.Orderid,
                    Serviceid = serviceDetail.Serviceid,
                    Quantity = request.Quantity
                };
                await _unitOfWork.Repository<Orderitem>().InsertAsync(newOrderItem, saveChanges: false);
                await _unitOfWork.SaveChangesAsync();

                // Nếu có Extras => Insert OrderExtra
                if (validExtras.Any())
                {
                    var newOrderExtras = validExtras.Select(ext => new Orderextra
                    {
                        Orderitemid = newOrderItem.Orderitemid,
                        Extraid = ext.Extraid
                    }).ToList();

                    await _unitOfWork.Repository<Orderextra>().InsertRangeAsync(newOrderExtras, saveChanges: false);
                }
            }

            // 6) Lưu thay đổi
            await _unitOfWork.SaveChangesAsync();
        }

        /// <summary>
        /// Hàm private so sánh xem 2 tập ExtraId có giống hệt nhau không.
        /// </summary>
        private bool ExtrasAreTheSame(ICollection<Orderextra> existingExtras, List<Guid> newExtraIds)
        {
            var existingIds = existingExtras.Select(e => e.Extraid).ToHashSet();
            var newIds = newExtraIds.ToHashSet();

            if (existingIds.Count != newIds.Count)
                return false;

            return existingIds.SetEquals(newIds);
        }


        //============================
        private decimal GetLegShippingFee(
            IDictionary<string, Area> districtToArea,          // tra cứu quận → Area
            string district,
            IEnumerable<Area> allShippingAreas)                // list để lấy max fallback
        {
            if (districtToArea.TryGetValue(district, out var area)
                && area.Shippingfee.HasValue && area.Shippingfee > 0)
            {
                return area.Shippingfee.Value;
            }

            // fallback: lấy giá cao nhất trong bảng ShippingFee
            return allShippingAreas.Where(a => a.Shippingfee.HasValue).Max(a => a.Shippingfee!.Value);
        }

        // Tính phí ship cho đơn hàng
        public async Task<CalculateShippingFeeResponse> CalculateShippingFeeAsync(CalculateShippingFeeRequest req)
        {
            // Lấy giờ VN để khớp với nghiệp vụ (dùng hàm util sẵn có)
            var nowVn = DateTime.UtcNow.AddHours(7);

            if (req.PickupTime < nowVn.AddMinutes(-10))
                throw new ApplicationException("pickupTime không hợp lệ (đã quá 10 phút).");

            var diffProcess = req.DeliveryTime - req.PickupTime;
            if (diffProcess.TotalHours < req.MinCompleteTime)
            {
                var unit = req.MinCompleteTime >= 24 ? "ngày" : "giờ";
                var min = req.MinCompleteTime / (unit == "ngày" ? 24 : 1);
                throw new ApplicationException(
                    $"Thời gian xử lý không đủ vì món đồ {req.ServiceName} có thời gian xử lý tối thiểu là {min} {unit}.");
            }

            // Kiểm tra địa chỉ pickup/delivery có tồn tại không
            var addressRepo = _unitOfWork.Repository<Address>();
            var pickupAddr = await addressRepo.FindAsync(req.PickupAddressId)
                             ?? throw new KeyNotFoundException("Pickup address not found.");
            var deliveryAddr = await addressRepo.FindAsync(req.DeliveryAddressId)
                             ?? throw new KeyNotFoundException("Delivery address not found.");

            /* ---------- 2. Lấy tọa độ & District ---------- */
            var pickupDistrict = await _mapboxService.GetDistrictFromCoordinatesAsync(
                                    pickupAddr.Latitude ?? 0, pickupAddr.Longitude ?? 0)
                                ?? "Unknown";
            var deliveryDistrict = await _mapboxService.GetDistrictFromCoordinatesAsync(
                                    deliveryAddr.Latitude ?? 0, deliveryAddr.Longitude ?? 0)
                                ?? "Unknown";

            /* -------- 3. Truy bảng Area lấy phí ship ---------- */
            var shippingAreas = _unitOfWork.Repository<Area>()
                                .GetAll()
                                .Where(a => a.Areatype.ToUpper() == "SHIPPINGFEE")
                                .ToList();          // EF -> RAM (chỉ 1 lần)

            if (!shippingAreas.Any(a => a.Shippingfee.HasValue))
                throw new ApplicationException("Bảng Area (ShippingFee) chưa có giá nào.");

            // Build dictionary: quận  →  Area
            var districtToArea = shippingAreas
                .Where(a => a.Districts != null)
                .SelectMany(a => a.Districts!.Select(d => new { District = d.Trim(), Area = a }))
                .ToDictionary(x => x.District, x => x.Area, StringComparer.OrdinalIgnoreCase);

            /* -------- 4. Tính phí từng chiều ---------- */
            decimal pickupLeg = GetLegShippingFee(districtToArea, pickupDistrict, shippingAreas);
            decimal deliverLeg = GetLegShippingFee(districtToArea, deliveryDistrict, shippingAreas);
            decimal shippingFee = pickupLeg + deliverLeg;

            /* -------- 5. Giảm phí theo EstimatedTotal ---------- */
            if (req.EstimatedTotal >= 1_000_000m) shippingFee = 0;
            else if (req.EstimatedTotal >= 350_000m) shippingFee *= 0.5m;

            /* ---------- Tính ApplicableFee ---------- */
            decimal applicableFee = 0m;
            if (diffProcess.TotalHours < 22)
                applicableFee = req.EstimatedTotal * 0.75m;
            else if (diffProcess.TotalHours < 46)
                applicableFee = req.EstimatedTotal * 0.5m;
            else if (diffProcess.TotalHours < 70)
                applicableFee = req.EstimatedTotal * 0.15m;

            return new CalculateShippingFeeResponse
            {
                ShippingFee = shippingFee,
                ApplicableFee = Math.Round(applicableFee, 0)   // làm tròn 0 đ nếu muốn
            };
        }

        /// <summary> Người dùng thêm sản phẩm vào giỏ hàng </summary>
        public async Task AddToCartAsync(HttpContext httpContext, AddToCartRequest request)
        {
            var userId = _util.GetCurrentUserIdOrThrow(httpContext);

            await _unitOfWork.BeginTransaction();
            try
            {
                await AddToCartNoTransactionAsync(userId, request);
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<CartResponse> UpdateCartItemAsync(HttpContext httpContext, UpdateCartItemRequest request)
        {
            return await UpdateCartItemAsync(_util.GetCurrentUserIdOrThrow(httpContext), request);
        }

        public async Task<CartResponse> UpdateCartItemAsync(Guid userId, UpdateCartItemRequest request)
        {
            await _unitOfWork.BeginTransaction();
            try
            {
                // 1) Tìm OrderItem -> Include(Order) để lấy ra order
                var orderItem = _unitOfWork.Repository<Orderitem>()
                    .GetAll()
                    .Include(oi => oi.Order)
                    .FirstOrDefault(oi => oi.Orderitemid == request.OrderItemId);

                if (orderItem == null)
                    throw new KeyNotFoundException("Order item not found.");

                // Kiểm tra có đúng user & đúng trạng thái
                if (orderItem.Order == null || orderItem.Order.Userid != userId)
                    throw new UnauthorizedAccessException("This item does not belong to the current user.");

                if (orderItem.Order.Currentstatus != "INCART")
                    throw new ApplicationException("Cannot edit items in a non-InCart order.");

                var order = orderItem.Order;

                // 2) Xóa OrderExtra liên quan đến item này
                var orderExtras = _unitOfWork.Repository<Orderextra>()
                    .GetAll()
                    .Where(e => e.Orderitemid == orderItem.Orderitemid)
                    .ToList();
                if (orderExtras.Any())
                {
                    await _unitOfWork.Repository<Orderextra>().DeleteRangeAsync(orderExtras, saveChanges: false);
                }

                // 3) Xóa chính OrderItem này
                await _unitOfWork.Repository<Orderitem>().DeleteAsync(orderItem, saveChanges: false);
                await _unitOfWork.SaveChangesAsync();

                // 4) Nếu Quantity = 0 => không thêm item mới nữa
                //    Kiểm tra xem order còn item nào không?
                if (request.Quantity == 0)
                {
                    var remainingItems = _unitOfWork.Repository<Orderitem>()
                        .GetAll()
                        .Where(oi => oi.Orderid == order.Orderid)
                        .Count();

                    if (remainingItems == 0)
                    {
                        // Xóa luôn order
                        await _unitOfWork.Repository<Order>().DeleteAsync(order, saveChanges: false);
                    }
                }
                else
                {
                    // 5) Nếu Quantity > 0 => Thêm lại item mới
                    var addRequest = new AddToCartRequest
                    {
                        ServiceDetailId = orderItem.Serviceid,
                        Quantity = request.Quantity,
                        ExtraIds = request.ExtraIds
                    };
                    // Hàm AddToCartNoTransactionAsync ta có thể dùng chung logic cũ
                    await AddToCartNoTransactionAsync(userId, addRequest);
                }

                // Lưu & commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();

                // 6) Sau khi cập nhật, trả về CartResponse
                //    Nếu order đã bị xóa, GetCartAsync sẽ ném NotFound => ta bắt & quăng "No cart found."
                var cart = await GetCartAsync(userId); // Gọi hàm đã có
                if (cart == null || cart.Items.Count == 0)
                {
                    throw new KeyNotFoundException("Delete item successfully! No cart found.");
                }
                return cart;
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<CartResponse> GetCartAsync(HttpContext httpContext)
        {
            return await GetCartAsync(_util.GetCurrentUserIdOrThrow(httpContext));
        }

        public async Task<CartResponse> GetCartAsync(Guid userId)
        {
            var order = _unitOfWork.Repository<Order>()
                .GetAll()
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Service)
                        .ThenInclude(sd => sd.Subservice)
                            .ThenInclude(si => si.Category)
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Orderextras)
                        .ThenInclude(oe => oe.Extra)
                .FirstOrDefault(o => o.Userid == userId && o.Currentstatus == "INCART");

            if (order == null) return null;

            decimal total = 0;
            int? maxMinCompleteTime = null;
            string? maxServiceName = null;
            var cartResponse = new CartResponse { OrderId = order.Orderid };

            foreach (var item in order.Orderitems)
            {
                var service = item.Service;
                var serviceId = item.Serviceid;
                var servicePrice = service?.Price ?? 0;
                var serviceName = service?.Name ?? "Unknown";

                var extraResponses = new List<CartExtraResponse>();
                decimal sumExtraPrices = 0;

                foreach (var oe in item.Orderextras)
                {
                    var extraPrice = oe.Extra?.Price ?? 0;

                    extraResponses.Add(new CartExtraResponse
                    {
                        ExtraId = oe.Extraid,
                        ExtraName = oe.Extra?.Name ?? "Unknown Extra",
                        ExtraPrice = extraPrice
                    });

                    sumExtraPrices += extraPrice;
                }

                var subTotal = (servicePrice + sumExtraPrices) * item.Quantity;
                total += subTotal;

                cartResponse.Items.Add(new CartItemResponse
                {
                    OrderItemId = item.Orderitemid,
                    ServiceId = serviceId,
                    ServiceName = serviceName,
                    ServicePrice = servicePrice,
                    Quantity = item.Quantity,
                    Extras = extraResponses,
                    SubTotal = subTotal
                });

                // ----- Tính MinCompleteTime lớn nhất -----
                var sub = service?.Subservice;
                if (sub?.Mincompletetime != null)
                {
                    if (maxMinCompleteTime == null || sub.Mincompletetime > maxMinCompleteTime)
                    {
                        maxMinCompleteTime = sub.Mincompletetime;
                        maxServiceName = sub.Category != null ? $"{sub.Category.Name} ({sub.Name})" : "Unknown";
                    }
                }
            }

            cartResponse.EstimatedTotal = total;
            cartResponse.MinCompleteTime = maxMinCompleteTime;
            cartResponse.ServiceName = maxServiceName;

            // ------ Trả pickup/delivery time ------
            var nowVn = _util.ConvertToVnTime(DateTime.UtcNow);

            // Đặt khung giờ phục vụ: 09:00–21:00 VN
            var openingTime = new DateTime(nowVn.Year, nowVn.Month, nowVn.Day, 9, 0, 0);
            var closingTime = new DateTime(nowVn.Year, nowVn.Month, nowVn.Day, 21, 0, 0);

            if (nowVn < openingTime || nowVn >= closingTime)
            {
                // Đặt đơn ngoài giờ, PickupTime là 09:00 sáng hôm sau
                var nextDayOpening = openingTime.AddDays(nowVn >= closingTime ? 1 : 0);
                if (nowVn < openingTime)
                    nextDayOpening = openingTime; // hôm nay
                cartResponse.PickupTime = nextDayOpening;
            }
            else
            {
                // Đặt đơn trong khung 9:00–21:00 VN, PickupTime là ngay bây giờ
                cartResponse.PickupTime = nowVn;
            }

            if (maxMinCompleteTime < 72)
            {
                cartResponse.DeliveryTime = cartResponse.PickupTime.AddDays(3);
            }
            else
            {
                cartResponse.DeliveryTime = cartResponse.PickupTime.AddHours(maxMinCompleteTime ?? 0); // cộng thêm thời gian hoàn thành
            }

            /* ---------- Lấy địa chỉ ưu tiên của User ---------- */
            var addresses = _unitOfWork.Repository<Address>()
                            .GetAll()
                            .Where(a => a.Userid == userId)
                            .ToList();

            Address? selectedAddr = addresses
                .FirstOrDefault(a => (a.Addresslabel ?? "")
                .Equals("Nhà riêng", StringComparison.OrdinalIgnoreCase))
                ?? addresses.FirstOrDefault();

            decimal shippingFee = 0m;

            if (selectedAddr != null)
            {
                cartResponse.addressCartResponse = new AddressCartResponse
                {
                    AddressId = selectedAddr.Addressid,
                    ContactName = selectedAddr.Contactname,
                    ContactPhone = selectedAddr.Contactphone,
                    AddressLabel = selectedAddr.Addresslabel,
                    DetailAddress = selectedAddr.Detailaddress,
                    Description = selectedAddr.Description
                };

                /* ----- 3. Tính ShippingFee dựa trên Area ----- */
                var district = await _mapboxService.GetDistrictFromCoordinatesAsync(
                                   selectedAddr.Latitude ?? 0, selectedAddr.Longitude ?? 0)
                               ?? "Unknown";

                var shippingAreas = _unitOfWork.Repository<Area>()
                                    .GetAll()
                                    .Where(a => a.Areatype.ToUpper() == "SHIPPINGFEE")
                                    .ToList();                         // LINQ-to-SQL an toàn

                var districtToArea = shippingAreas
                    .Where(a => a.Districts != null)
                    .SelectMany(a => a.Districts!.Select(d => new { d, a.Name }))
                    .ToDictionary(x => x.d, x => x.Name, StringComparer.OrdinalIgnoreCase);

                var areaName = districtToArea.TryGetValue(district, out var an) ? an : "Unknown";

                decimal CalcLegFee(string name) => name switch
                {
                    "Khu vực 1" => 30_000m,
                    "Khu vực 2" => 40_000m,
                    "Khu vực 3" => 50_000m,
                    _ => 50_000m
                };

                // Hai chiều (nhận & giao) cùng 1 khu vực
                shippingFee = CalcLegFee(areaName) * 2;

                // Giảm theo EstimatedTotal
                if (total >= 1_000_000m) shippingFee = 0;
                else if (total >= 350_000m) shippingFee *= 0.5m;
            }
            /* Nếu user chưa có địa chỉ → shippingFee mặc định = 0 & addressCartResponse = null */

            cartResponse.ShippingFee = shippingFee;
            cartResponse.ApplicableFee = 0m;

            return cartResponse;
        }

        public async Task<string> PlaceOrderAsync(HttpContext httpContext, PlaceOrderRequest request)
        {
            var userId = _util.GetCurrentUserIdOrThrow(httpContext);

            // Bắt đầu transaction
            await _unitOfWork.BeginTransaction();

            try
            {
                // 1) Tìm Order INCART (Eager load Orderitems + Extras + Service)
                var order = _unitOfWork.Repository<Order>()
                    .GetAll()
                    .Include(o => o.Orderitems)
                        .ThenInclude(oi => oi.Orderextras)
                        .ThenInclude(oe => oe.Extra)
                    .Include(o => o.Orderitems)
                        .ThenInclude(oi => oi.Service)
                    .FirstOrDefault(o => o.Userid == userId && o.Currentstatus == "INCART");

                if (order == null)
                {
                    throw new KeyNotFoundException("No 'INCART' order found to place.");
                }

                // 2) Lấy thông tin Address pickup & delivery
                //    (với AddressId được gửi trong request)
                var pickupAddress = _unitOfWork.Repository<Address>()
                    .GetAll()
                    .FirstOrDefault(a => a.Addressid == request.PickupAddressId && a.Userid == userId);
                if (pickupAddress == null)
                {
                    throw new KeyNotFoundException("Pickup address not found for current user.");
                }

                var deliveryAddress = _unitOfWork.Repository<Address>()
                    .GetAll()
                    .FirstOrDefault(a => a.Addressid == request.DeliveryAddressId && a.Userid == userId);
                if (deliveryAddress == null)
                {
                    throw new KeyNotFoundException("Delivery address not found for current user.");
                }

                // 3) Gán các thông tin Pickup/Delivery vào Order
                order.Pickuplabel = pickupAddress.Addresslabel;
                order.Pickupname = pickupAddress.Contactname;
                order.Pickupphone = pickupAddress.Contactphone;
                order.Pickupaddressdetail = pickupAddress.Detailaddress;
                order.Pickupdescription = pickupAddress.Description;
                order.Pickuplatitude = pickupAddress.Latitude;
                order.Pickuplongitude = pickupAddress.Longitude;

                order.Deliverylabel = deliveryAddress.Addresslabel;
                order.Deliveryname = deliveryAddress.Contactname;
                order.Deliveryphone = deliveryAddress.Contactphone;
                order.Deliveryaddressdetail = deliveryAddress.Detailaddress;
                order.Deliverydescription = deliveryAddress.Description;
                order.Deliverylatitude = deliveryAddress.Latitude;
                order.Deliverylongitude = deliveryAddress.Longitude;

                // 4) Gán thời gian pickup/delivery
                order.Pickuptime = _util.ConvertVnDateTimeToUtc(request.Pickuptime);
                order.Deliverytime = _util.ConvertVnDateTimeToUtc(request.Deliverytime);

                // 5) Cập nhật giá cho các OrderItem và OrderExtra
                decimal basePriceSum = 0m;

                foreach (var item in order.Orderitems)
                {
                    // Lấy giá hiện tại của service
                    var servicePrice = item.Service?.Price ?? 0;
                    item.Baseprice = servicePrice; // Gán baseprice

                    // Tính tổng extras
                    decimal sumExtraPrices = 0;
                    foreach (var oe in item.Orderextras)
                    {
                        var extraPrice = oe.Extra?.Price ?? 0;
                        oe.Extraprice = extraPrice;
                        sumExtraPrices += extraPrice;

                        // Đánh dấu EntityState.Modified, 
                        // hoặc dùng UpdateAsync(oe, saveChanges:false).
                        _unitOfWork.DbContext.Entry(oe).State = EntityState.Modified;
                    }

                    // SubTotal cho item
                    var subTotal = (servicePrice + sumExtraPrices) * item.Quantity;
                    basePriceSum += subTotal;

                    // Đánh dấu EntityState.Modified, 
                    // hoặc dùng UpdateAsync(item, saveChanges:false).
                    _unitOfWork.DbContext.Entry(item).State = EntityState.Modified;
                }

                // 6) Tính tổng theo công thức
                //    total = basePriceSum + shippingFee + shippingDiscount + applicableFee + discount
                decimal shippingDiscount = request.Shippingdiscount ?? 0;
                decimal applicableFee = request.Applicablefee ?? 0;
                decimal discount = request.Discount ?? 0;

                decimal finalTotal = basePriceSum
                                    + request.Shippingfee
                                    + shippingDiscount
                                    + applicableFee
                                    + discount;

                // So sánh finalTotal với request.Total
                //   do decimal so sánh, có thể bạn cần Round / tolerance
                //   ở đây mình so sánh chính xác
                if (finalTotal != request.Total)
                {
                    throw new ApplicationException(
                        $"Total mismatch. Server computed: {finalTotal}, client sent: {request.Total}.");
                }

                // 7) Gán các trường còn lại vào Order
                order.Shippingfee = request.Shippingfee;
                order.Shippingdiscount = shippingDiscount;
                order.Applicablefee = applicableFee;
                order.Discount = discount;
                order.Totalprice = finalTotal;
                order.Currentstatus = "PENDING";
                order.Createdat = DateTime.UtcNow; //thay đổi ngày tạo thành ngày place order

                // Update Order
                await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);

                // 8) Tạo OrderStatusHistory: "PENDING"
                var newStatusHistory = new Orderstatushistory
                {
                    Orderid = order.Orderid,
                    Status = "PENDING",
                    Statusdescription = "Đặt hàng thành công, chờ xác nhận",
                    Notes = request.Note,
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(newStatusHistory, saveChanges: false);

                // 9) Lưu các thay đổi & commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();

                // Trả về OrderId
                return order.Orderid;
            }
            catch
            {
                // Nếu có lỗi => rollback
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<CartResponse> ReorderAsync(HttpContext httpContext, string orderId)
        {
            // Lấy userId từ HttpContext
            var userId = _util.GetCurrentUserIdOrThrow(httpContext);

            // Kiểm tra xem người dùng đã có giỏ hàng (order ở trạng thái "INCART") hay chưa
            var currentCart = _unitOfWork.Repository<Order>()
                .GetAll()
                .FirstOrDefault(o => o.Userid == userId && o.Currentstatus == "INCART");

            if (currentCart != null)
            {
                throw new ApplicationException("Bạn đang có một giỏ hàng đang hoạt động. Vui lòng hoàn tất hoặc hủy giỏ hàng hiện tại trước khi đặt lại đơn hàng (re-order).");
            }


            // Lấy thông tin order cũ, include các OrderItem và Orderextra nếu có
            var oldOrder = _unitOfWork.Repository<Order>()
                .GetAll()
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Orderextras)
                .FirstOrDefault(o => o.Orderid == orderId);

            if (oldOrder == null)
                throw new KeyNotFoundException("Order không tồn tại.");

            if (oldOrder.Userid != userId)
                throw new UnauthorizedAccessException("Order không thuộc về người dùng hiện tại.");

            // Bắt đầu transaction
            await _unitOfWork.BeginTransaction();
            try
            {
                //Duyệt qua các OrderItem của order cũ
                foreach (var orderItem in oldOrder.Orderitems)
                {

                    var extraIds = orderItem.Orderextras != null
                        ? orderItem.Orderextras.Select(e => e.Extraid).ToList()
                        : new List<Guid>();

                    var addRequest = new AddToCartRequest
                    {
                        ServiceDetailId = orderItem.Serviceid,
                        Quantity = orderItem.Quantity,
                        ExtraIds = extraIds
                    };

                    //Gọi hàm AddToCartNoTransactionAsync để thêm item vào cart
                    await AddToCartNoTransactionAsync(userId, addRequest);
                }

                //Lưu thay đổi và commit transaction
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();

                //Lấy lại thông tin cart và trả về
                var cart = await GetCartAsync(httpContext);
                if (cart == null || cart.Items.Count == 0)
                {
                    throw new KeyNotFoundException("Reorder thành công nhưng không tìm thấy cart.");
                }

                return cart;
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<List<UserOrderResponse>> GetUserOrdersAsync(HttpContext httpContext)
        {
            var userId = _util.GetCurrentUserIdOrThrow(httpContext);

            // Lấy các order (ngoại trừ INCART), sắp xếp theo CreatedAt desc
            // Eager load: Orderitems -> Service -> Subservice -> Category 
            var orders = _unitOfWork.Repository<Order>()
                .GetAll()
                .Where(o => o.Userid == userId && o.Currentstatus != "INCART")
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Service)
                        .ThenInclude(s => s.Subservice)
                            .ThenInclude(sb => sb.Category)
                .OrderByDescending(o => o.Createdat)
                .ToList();

            var result = new List<UserOrderResponse>();

            foreach (var order in orders)
            {
                // "OrderName": lấy danh mục (category) của từng service -> gộp bằng dấu phẩy
                //   Mỗi OrderItem -> Service -> Subservice -> Category -> Name
                //   Lọc null + distinct => ghép lại
                var categoryNames = order.Orderitems
                    .Select(oi => oi.Service?.Subservice?.Category?.Name)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct()
                    .ToList();

                // Gộp thành 1 chuỗi, vd: "Giặt giày, Giặt sấy"
                var orderName = string.Join(", ", categoryNames);

                // Số lượng service = số dòng orderItem
                var serviceCount = order.Orderitems.Count;

                var item = new UserOrderResponse
                {
                    OrderId = order.Orderid,
                    OrderName = orderName,
                    ServiceCount = serviceCount,
                    TotalPrice = order.Totalprice,
                    OrderedDate = _util.ConvertToVnTime(order.Createdat ?? DateTime.UtcNow),
                    OrderStatus = order.Currentstatus
                };

                result.Add(item);
            }

            return result;
        }

        public async Task<PaginationResult<UserOrderResponse>> GetAllOrdersAsync(HttpContext httpContext, string? status, int page, int pageSize)
        {
            // Giả sử ta chỉ cho Admin/Staff truy cập, 
            // check logic, ném exception hoặc check Roles ở Controller
            // (Ở đây có thể check role, hoặc tin tưởng Controller đã [Authorize(Roles="...")])

            // 1) Lấy query Orders (ngoại trừ INCART), 
            //    Nếu có status => lọc
            var ordersQuery = _unitOfWork.Repository<Order>()
                .GetAll()
                .Where(o => o.Currentstatus != "INCART");

            if (!string.IsNullOrEmpty(status))
            {
                // Lọc theo status
                ordersQuery = ordersQuery.Where(o => o.Currentstatus == status);
            }

            // 2) Eager load: Orderitems -> Service -> Subservice -> Category
            // Rồi sắp xếp "sớm nhất" (CreatedAt ascending)
            var query = ordersQuery
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Service)
                        .ThenInclude(s => s.Subservice)
                            .ThenInclude(sb => sb.Category)
                .OrderByDescending(o => o.Createdat);

            // 3) Phân trang => Tính totalRecords
            var totalRecords = query.Count(); // Đếm tổng

            // Skip & Take
            var orders = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList(); // load về danh sách

            // 4) Duyệt từng order, map sang UserOrderResponse
            var resultList = new List<UserOrderResponse>();

            foreach (var order in orders)
            {
                // Gom tên category
                var categoryNames = order.Orderitems
                    .Select(oi => oi.Service?.Subservice?.Category?.Name)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct()
                    .ToList();

                resultList.Add(new UserOrderResponse
                {
                    OrderId = order.Orderid,
                    OrderName = string.Join(", ", categoryNames),
                    ServiceCount = order.Orderitems.Count,
                    TotalPrice = order.Totalprice,
                    OrderedDate = _util.ConvertToVnTime(order.Createdat ?? DateTime.UtcNow),
                    OrderStatus = order.Currentstatus
                });
            }

            // 5) Đóng gói kiểu PaginationResult
            var paginationResult = new PaginationResult<UserOrderResponse>(
                data: resultList,
                totalRecords: totalRecords,
                currentPage: page,
                pageSize: pageSize
            );

            return paginationResult;
        }

        public async Task<UserOrderResponse> GetOrderByIdAsync(HttpContext httpContext, string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
                throw new ArgumentException("orderId is required.");

            // 1) Eager-load đầy đủ thông tin cần thiết
            var order = _unitOfWork.Repository<Order>()
                         .GetAll()
                         .Where(o => o.Orderid == orderId && o.Currentstatus != "INCART")
                         .Include(o => o.Orderitems)
                             .ThenInclude(oi => oi.Service)
                                 .ThenInclude(s => s.Subservice)
                                     .ThenInclude(sb => sb.Category)
                         .FirstOrDefault();

            if (order == null)
                throw new KeyNotFoundException($"Order '{orderId}' not found.");

            // 2) Gom tên Category cho OrderName
            var categoryNames = order.Orderitems
                .Select(oi => oi.Service?.Subservice?.Category?.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToList();

            return new UserOrderResponse
            {
                OrderId = order.Orderid,
                OrderName = string.Join(", ", categoryNames),
                ServiceCount = order.Orderitems.Count,
                TotalPrice = order.Totalprice,
                OrderedDate = _util.ConvertToVnTime(order.Createdat ?? DateTime.UtcNow),
                OrderStatus = order.Currentstatus
            };
        }

        public async Task<OrderDetailCustomResponse> GetOrderDetailCustomAsync(HttpContext httpContext, string orderId)
        {
            // 1) Eager load
            var order = _unitOfWork.Repository<Order>()
                .GetAll()
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Orderextras)
                        .ThenInclude(oe => oe.Extra)
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Service)
                .Include(o => o.Orderstatushistories)
                .FirstOrDefault(o => o.Orderid == orderId);

            if (order == null || order.Currentstatus == "INCART")
                throw new KeyNotFoundException("Order not found.");

            // (Tuỳ logic: nếu user role => check order.Userid == currentUserId,
            // Admin/Staff => có thể xem tất cả, v.v. 
            // Ở đây giả sử cho user xem đơn của họ.)
            var currentUserId = _util.GetCurrentUserIdOrThrow(httpContext);
            // if (order.Userid != currentUserId) throw new UnauthorizedAccessException("Not your order.");

            // 2) Map Order => response
            var response = new OrderDetailCustomResponse
            {
                OrderId = order.Orderid,
                UserId = order.Userid,
                PickupLabel = order.Pickuplabel,
                PickupName = order.Pickupname,
                PickupPhone = order.Pickupphone,
                PickupAddressDetail = order.Pickupaddressdetail,
                PickupDescription = order.Pickupdescription,
                PickupLatitude = order.Pickuplatitude,
                PickupLongitude = order.Pickuplongitude,

                DeliveryLabel = order.Deliverylabel,
                DeliveryName = order.Deliveryname,
                DeliveryPhone = order.Deliveryphone,
                DeliveryAddressDetail = order.Deliveryaddressdetail,
                DeliveryDescription = order.Deliverydescription,
                DeliveryLatitude = order.Deliverylatitude,
                DeliveryLongitude = order.Deliverylongitude,

                PickupTime = _util.ConvertToVnTime(order.Pickuptime ?? DateTime.UtcNow),
                DeliveryTime = _util.ConvertToVnTime(order.Deliverytime ?? DateTime.UtcNow),
                CreatedAt = _util.ConvertToVnTime(order.Createdat ?? DateTime.UtcNow),
            };

            // 3) Lấy notes từ OrderStatusHistory => status = "PENDING" (nếu có)
            var pendingRow = order.Orderstatushistories
                .FirstOrDefault(sh => sh.Status == "PENDING");
            if (pendingRow != null)
            {
                response.Notes = pendingRow.Notes;
            }

            // 4) Map OrderSummary
            //    Tính subTotal cho mỗi item, sum => estimatedTotal
            decimal estimatedTotal = 0;
            var orderSummary = new OrderSummaryResponse
            {
                ShippingFee = order.Shippingfee,
                ShippingDiscount = order.Shippingdiscount,
                ApplicableFee = order.Applicablefee,
                Discount = order.Discount,
                Otherprice = order.Otherprice,
                OtherPriceNote = order.Noteforotherprice,
                TotalPrice = order.Totalprice
            };

            foreach (var item in order.Orderitems)
            {
                var serviceName = item.Service?.Name ?? "Unknown";
                var servicePrice = item.Service?.Price ?? 0;

                decimal sumExtras = 0;
                var extraList = new List<ExtraSummary>();

                foreach (var oe in item.Orderextras)
                {
                    var extraName = oe.Extra?.Name ?? "Unknown Extra";
                    var extraPrice = oe.Extra?.Price ?? 0;
                    sumExtras += extraPrice;

                    extraList.Add(new ExtraSummary
                    {
                        OrderextraId = oe.Extraid,
                        ExtraName = extraName,
                        ExtraPrice = extraPrice
                    });
                }

                var subTotal = (servicePrice + sumExtras) * item.Quantity;
                estimatedTotal += subTotal;

                orderSummary.Items.Add(new OrderItemSummary
                {
                    OrderitemId = item.Orderitemid,
                    ServiceName = serviceName,
                    ServicePrice = servicePrice,
                    Quantity = item.Quantity,
                    Extras = extraList,
                    SubTotal = subTotal
                });
            }
            orderSummary.EstimatedTotal = estimatedTotal;
            response.OrderSummary = orderSummary;

            // 5) CurrentOrderStatus
            //    Tìm row trong OrderStatusHistory => `Status = order.Currentstatus`
            //    Lấy statusDescription, createdAt => lastUpdate
            var currentStatus = order.Currentstatus;
            var rowCurrentStatus = order.Orderstatushistories
                .Where(sh => sh.Status == currentStatus)
                .OrderByDescending(sh => sh.Createdat) // Nếu có nhiều row, lấy row mới nhất
                .FirstOrDefault();

            var cos = new CurrentOrderStatusResponse
            {
                CurrentStatus = currentStatus,
                StatusDescription = rowCurrentStatus?.Statusdescription,
                LastUpdate = _util.ConvertToVnTime(rowCurrentStatus.Createdat.Value)
            };

            response.CurrentOrderStatus = cos;

            return response;
        }

        public async Task<List<OrderStatusHistoryItemResponse>> GetOrderStatusHistoryAsync(HttpContext httpContext, string orderId)
        {
            // 1) Kiểm tra Order có tồn tại hay không
            var order = _unitOfWork.Repository<Order>()
                .GetAll()
                .FirstOrDefault(o => o.Orderid == orderId);

            if (order == null || order.Currentstatus == "INCART")
                throw new KeyNotFoundException("Order not found.");

            // (Nếu bạn cần giới hạn user chỉ được xem order của chính họ,
            //  kiểm tra order.Userid == currentUserId, v.v.)

            // 2) Truy vấn Orderstatushistory theo OrderId, 
            //    Eager load UpdatedbyNavigation (là user) + Orderphotos
            //    sắp xếp CreatedAt desc => bản mới nhất trước
            var histories = _unitOfWork.Repository<Orderstatushistory>()
                .GetAll()
                .Where(sh => sh.Orderid == orderId)
                .Include(sh => sh.UpdatedbyNavigation) // Lấy thông tin user
                .Include(sh => sh.Orderphotos)         // Để biết có media ảnh không
                .OrderBy(sh => sh.Createdat)
                .ToList();

            // 3) Map sang DTO
            var result = new List<OrderStatusHistoryItemResponse>();

            foreach (var h in histories)
            {
                // Kiểm tra user Updatedby
                UpdatedByUser? updatedBy = null;
                if (h.UpdatedbyNavigation != null)
                {
                    updatedBy = new UpdatedByUser
                    {
                        UserId = h.UpdatedbyNavigation.Userid,
                        FullName = h.UpdatedbyNavigation.Fullname,
                        PhoneNumber = h.UpdatedbyNavigation.Phonenumber
                    };
                }

                // Kiểm tra media
                var containMedia = (h.Orderphotos != null && h.Orderphotos.Any())
                    ? true
                    : false;

                // Tạo item
                var item = new OrderStatusHistoryItemResponse
                {
                    StatusHistoryId = h.Statushistoryid,
                    Status = h.Status,
                    StatusDescription = h.Statusdescription,
                    Notes = h.Notes,
                    UpdatedBy = updatedBy,
                    CreatedAt = _util.ConvertToVnTime((DateTime)h.Createdat),
                    ContainMedia = containMedia
                };

                result.Add(item);
            }

            return result;
        }

        public async Task<Guid> GetCustomerIdByOrderAsync(string orderId)
        {
            var order = _unitOfWork.Repository<Order>()
                .GetAll()
                .FirstOrDefault(o => o.Orderid == orderId);

            if (order == null)
                throw new KeyNotFoundException("Không tìm thấy đơn hàng.");

            return order.Userid; //  Trường chứa customer ID
        }

        public async Task<Guid> GetCustomerIdByAssignmentAsync(Guid assignmentId)
        {
            var assignment = _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAll()
                .FirstOrDefault(a => a.Assignmentid == assignmentId);

            if (assignment == null)
                throw new KeyNotFoundException("Không tìm thấy thông tin phân công đơn hàng.");

            // Truy ngược lên Order để lấy UserId
            var order = _unitOfWork.Repository<Order>()
                .GetAll()
                .FirstOrDefault(o => o.Orderid == assignment.Orderid);

            if (order == null)
                throw new KeyNotFoundException("Không tìm thấy đơn hàng tương ứng với phân công.");

            return order.Userid;
        }

        public async Task<string> GetOrderIdByAssignmentAsync(Guid assignmentId)
        {
            var assignment = await _unitOfWork.Repository<Orderassignmenthistory>().FindAsync(assignmentId);
            if (assignment == null || string.IsNullOrWhiteSpace(assignment.Orderid))
            {
                throw new KeyNotFoundException("Không tìm thấy thông tin đơn hàng.");
            }

            return assignment.Orderid; // Chính là mã đơn như 250407GC1PHG
        }

        public async Task<int> CompleteOrderAsync(HttpContext httpContext, string orderId)
        {
            var userId = _util.GetCurrentUserIdOrThrow(httpContext);

            await _unitOfWork.BeginTransaction();
            try
            {
                /* ---------- 1. Lấy Order ---------- */
                var order = _unitOfWork.Repository<Order>()
                            .GetAll()
                            .Include(o => o.Orderitems)
                                .ThenInclude(oi => oi.Service)
                                    .ThenInclude(s => s.Subservice)
                                        .ThenInclude(ss => ss.Category)
                            .FirstOrDefault(o => o.Orderid == orderId)
                            ?? throw new KeyNotFoundException("Order not found.");

                if (order.Currentstatus != OrderStatusEnum.DELIVERED.ToString())
                    throw new ApplicationException("Order is not in DELIVERED status.");

                /* ---------- 3. Cập nhật Order ---------- */
                order.Currentstatus = OrderStatusEnum.COMPLETED.ToString();
                await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);

                /* ---------- 4. Thêm OrderStatusHistory ---------- */
                var history = new Orderstatushistory
                {
                    Statushistoryid = Guid.NewGuid(),
                    Orderid = orderId,
                    Status = OrderStatusEnum.COMPLETED.ToString(),
                    Statusdescription = "Đơn hàng đã hoàn thành",
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(history, saveChanges: false);

                /* ---------- 5. Cộng RewardPoints ---------- */
                // Công thức: chi tiêu = totalprice + discount - applicablefee - shippingfee
                decimal spend = (order.Totalprice ?? 0)
                              + (order.Discount ?? 0)
                              - (order.Applicablefee ?? 0)
                              - (order.Shippingfee ?? 0);

                if (spend < 0) spend = 0;                    // an toàn

                // Quy đổi điểm: 10.000 → 1 điểm, làm tròn 5.000 lên
                int points = (int)Math.Floor((spend + 5000) / 10000);

                if (points > 0)
                {
                    var user = await _unitOfWork.Repository<User>().FindAsync(userId);

                    user.Rewardpoints = user.Rewardpoints + points;
                    await _unitOfWork.Repository<User>().UpdateAsync(user, saveChanges: false);
                }

                // get category name từ Orderitems
                var categoryNames = order.Orderitems
                    .Select(oi => oi.Service?.Subservice?.Category?.Name)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct()
                    .ToList();

                // Gộp thành 1 chuỗi, vd: "Giặt giày, Giặt sấy"
                var orderName = string.Join(", ", categoryNames);

                var rewardHistory = new Rewardhistory()
                {
                    Rewardhistoryid = Guid.NewGuid(),
                    Userid = userId,
                    Orderid = orderId,
                    Ordername = orderName,
                    Points = points,
                    Datecreated = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Rewardhistory>().InsertAsync(rewardHistory, saveChanges: false);

                // Lưu jobId để lát nữa huỷ
                var jobId = order.AutoCompleteJobId;

                /* ---------- 6. Commit ---------- */
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();

                // Huỷ background-job (ngoài transaction DB)
                if (!string.IsNullOrEmpty(jobId))
                    _jobService.CancelAutoComplete(jobId);

                return points; // Trả về số điểm cộng thêm cho user
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<PaginationResult<AssignedOrderDetailResponse>> GetAssignedPickupsAsync(HttpContext ctx, int page, int pageSize)
        {
            var query = _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAll()
                .AsNoTracking()
                .Where(ah => ah.Status == AssignStatusEnum.ASSIGNED_PICKUP.ToString()
                             && ah.Completedat == null)
                .Include(ah => ah.Order)
                    .ThenInclude(o => o.User)
                .Include(ah => ah.AssignedtoNavigation)
                .OrderByDescending(ah => ah.Assignedat);
            int total = await query.CountAsync();
            var rows = await query.Skip((page - 1) * pageSize)
                                   .Take(pageSize)
                                   .ToListAsync();

            var list = rows.Select(r =>
            {
                var order = r.Order;
                var customer = order.User;
                var driver = r.AssignedtoNavigation;

                return new AssignedOrderDetailResponse
                {
                    AssignmentId = r.Assignmentid,
                    OrderId = order.Orderid,

                    CustomerFullname = customer.Fullname,
                    CustomerPhone = customer.Phonenumber,
                    Address = order.Pickupaddressdetail,
                    TotalPrice = order.Totalprice,

                    DriverFullname = driver?.Fullname,
                    DriverPhone = driver?.Phonenumber,

                    AssignedAt = _util.ConvertToVnTime(r.Assignedat ?? DateTime.UtcNow),
                    Status = r.Status
                };
            }).ToList();

            return new PaginationResult<AssignedOrderDetailResponse>(list, total, page, pageSize);
        }

        public async Task<PaginationResult<AssignedOrderDetailResponse>> GetAssignedDeliveriesAsync(HttpContext ctx, int page, int pageSize)
        {
            var query = _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAll()
                .AsNoTracking()
                .Where(ah => ah.Status == AssignStatusEnum.ASSIGNED_DELIVERY.ToString()
                             && ah.Completedat == null)
                .Include(ah => ah.Order)
                    .ThenInclude(o => o.User)
                .Include(ah => ah.AssignedtoNavigation)
                .OrderByDescending(ah => ah.Assignedat);

            int total = await query.CountAsync();
            var rows = await query.Skip((page - 1) * pageSize)
                                   .Take(pageSize)
                                   .ToListAsync();

            var list = rows.Select(r =>
            {
                var order = r.Order;
                var customer = order.User;
                var driver = r.AssignedtoNavigation;

                return new AssignedOrderDetailResponse
                {
                    AssignmentId = r.Assignmentid,
                    OrderId = order.Orderid,

                    CustomerFullname = customer.Fullname,
                    CustomerPhone = customer.Phonenumber,
                    Address = order.Deliveryaddressdetail,
                    TotalPrice = order.Totalprice,

                    DriverFullname = driver?.Fullname,
                    DriverPhone = driver?.Phonenumber,

                    AssignedAt = _util.ConvertToVnTime(r.Assignedat ?? DateTime.UtcNow),
                    Status = r.Status
                };
            }).ToList();

            return new PaginationResult<AssignedOrderDetailResponse>(list, total, page, pageSize);
        }
    }
}