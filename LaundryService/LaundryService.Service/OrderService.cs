using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Pagination;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;

        public OrderService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Lấy userId từ JWT token. Ném exception nếu không hợp lệ.
        /// </summary>
        private Guid GetCurrentUserIdOrThrow(HttpContext httpContext)
        {
            var userIdClaim = httpContext?.User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var userId) && userId != Guid.Empty)
            {
                return userId;
            }
            throw new UnauthorizedAccessException("Invalid token: Cannot retrieve userId.");
        }

        public async Task AddToCartAsync(HttpContext httpContext, AddToCartRequest request)
        {
            // 1) Lấy userId từ token
            var userId = GetCurrentUserIdOrThrow(httpContext);

            // 2) Bắt đầu transaction
            await _unitOfWork.BeginTransaction();

            try
            {
                // 3) Tìm ServiceDetail
                var serviceDetail = await _unitOfWork
                    .Repository<Servicedetail>()
                    .GetAsync(s => s.Serviceid == request.ServiceDetailId);

                if (serviceDetail == null)
                    throw new KeyNotFoundException("Service detail not found.");

                // 4) Tìm Order INCART
                var order = _unitOfWork.Repository<Order>()
                    .GetAll()
                    .FirstOrDefault(o => o.Userid == userId && o.Currentstatus == "INCART");

                if (order == null)
                {
                    // Chưa có => tạo mới
                    order = new Order
                    {
                        Userid = userId,
                        Currentstatus = "INCART",
                        Createdat = DateTime.UtcNow
                    };
                    await _unitOfWork.Repository<Order>().InsertAsync(order);
                    await _unitOfWork.SaveChangesAsync();
                }

                // 5) Kiểm tra xem ExtraIds có hợp lệ không
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
                        throw new ApplicationException($"Some extras not found in database: {string.Join(", ", invalidIds)}");
                    }
                }

                // 6) Tìm xem có OrderItem nào trùng ServiceDetail và trùng EXACT Extras
                //    (Sẽ load kèm OrderExtras để so sánh)
                //    Chỉ load những OrderItem có ServiceID == serviceDetail.Serviceid
                //    Rồi so sánh ExtraIds
                var orderItemsSameService = _unitOfWork.Repository<Orderitem>()
                    .GetAll()
                    .Where(oi => oi.Orderid == order.Orderid && oi.Serviceid == serviceDetail.Serviceid)
                    .ToList();

                Orderitem? matchedItem = null;

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

                if (matchedItem != null)
                {
                    // 7) Nếu có matchedItem => tăng Quantity
                    matchedItem.Quantity += request.Quantity;
                    await _unitOfWork.Repository<Orderitem>().UpdateAsync(matchedItem, saveChanges: false);
                }
                else
                {
                    // 8) Nếu không có => tạo OrderItem mới
                    var newOrderItem = new Orderitem
                    {
                        Orderid = order.Orderid,
                        Serviceid = serviceDetail.Serviceid,
                        Quantity = request.Quantity
                    };
                    await _unitOfWork.Repository<Orderitem>().InsertAsync(newOrderItem, saveChanges: false);
                    await _unitOfWork.SaveChangesAsync();

                    // 9) Thêm OrderExtra (nếu có)
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

                // 10) Lưu changes & commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                // rollback
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// So sánh xem 2 tập ExtraId có giống hệt nhau không.
        /// </summary>
        private bool ExtrasAreTheSame(ICollection<Orderextra> existingExtras, List<Guid> newExtraIds)
        {
            var existingIds = existingExtras.Select(e => e.Extraid).ToHashSet();
            var newIds = newExtraIds.ToHashSet();

            if (existingIds.Count != newIds.Count)
                return false;

            return existingIds.SetEquals(newIds);
        }

        public async Task<CartResponse> GetCartAsync(HttpContext httpContext)
        {
            var userId = GetCurrentUserIdOrThrow(httpContext);

            var order = _unitOfWork.Repository<Order>()
                .GetAll()
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Service)
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Orderextras)
                    .ThenInclude(oe => oe.Extra)
                .FirstOrDefault(o => o.Userid == userId && o.Currentstatus == "INCART");

            if (order == null) throw new KeyNotFoundException("No cart found.");

            decimal total = 0;
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
                    var extraName = oe.Extra?.Name ?? "Unknown Extra";

                    extraResponses.Add(new CartExtraResponse
                    {
                        ExtraId = oe.Extraid,
                        ExtraName = extraName,
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
            }

            cartResponse.EstimatedTotal = total;
            return cartResponse;
        }

        public async Task<Guid> PlaceOrderAsync(HttpContext httpContext, PlaceOrderRequest request)
        {
            var userId = GetCurrentUserIdOrThrow(httpContext);

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
                order.Pickuptime = request.Pickuptime;
                order.Deliverytime = request.Deliverytime;

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

        public async Task<List<UserOrderResponse>> GetUserOrdersAsync(HttpContext httpContext)
        {
            var userId = GetCurrentUserIdOrThrow(httpContext);

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
                    OrderedDate = ConvertToVnTime(order.Createdat ?? DateTime.UtcNow),
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
                    OrderedDate = ConvertToVnTime(order.Createdat ?? DateTime.UtcNow),
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

        // Hàm convert DateTime UTC sang giờ Việt Nam (UTC+7)
        private DateTime ConvertToVnTime(DateTime utcDateTime)
        {
            // Cách 1: utcDateTime.AddHours(7)
            // Cách 2: Sử dụng TimeZoneInfo 
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, timeZone);
        }
    }
}