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
        private readonly IUtil _util;
        private readonly IConfiguration _configuration;
        private readonly IMapboxService _mapboxService;

        public OrderService(IUnitOfWork unitOfWork, IUtil util, IConfiguration configuration, IMapboxService mapboxService)
        {
            _unitOfWork = unitOfWork;
            _util = util;
            _configuration = configuration;
            _mapboxService = mapboxService;
        }

        /// <summary>
        /// Hàm private thêm sản phẩm vào giỏ hàng mà không cần transaction.
        /// </summary>
        private async Task AddToCartNoTransactionAsync(Guid userId, AddToCartRequest request)
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
            var userId = _util.GetCurrentUserIdOrThrow(httpContext);

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
                var cart = await GetCartAsync(httpContext); // Gọi hàm đã có
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
            var userId = _util.GetCurrentUserIdOrThrow(httpContext);

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

        public async Task<PaginationResult<UserOrderResponse>> GetPendingOrdersForStaffAsync(HttpContext httpContext, int page, int pageSize)
        {
            // Chỉ load các order có status = "PENDING"
            // và KHÔNG có OrderAssignmentHistory nào có status = "processing"

            // 1) Tạo query
            var query = _unitOfWork.Repository<Order>()
                .GetAll()
                // status = "PENDING"
                .Where(o => o.Currentstatus == "PENDING")
                // Bỏ qua order nào có trong Orderassignmenthistory với status="processing"
                .Where(o => !o.Orderassignmenthistories.Any(ah => ah.Status == "PROCESSING"))
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Service)
                        .ThenInclude(s => s.Subservice)
                            .ThenInclude(sb => sb.Category)
                .OrderByDescending(o => o.Createdat); // sắp xếp tuỳ yêu cầu, ở đây mới nhất trước

            // 2) Đếm tổng
            var totalRecords = query.Count();

            // 3) Skip & Take
            var orders = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 4) Map Order -> UserOrderResponse
            var resultList = new List<UserOrderResponse>();

            foreach (var order in orders)
            {
                // Lấy tên category
                var categoryNames = order.Orderitems
                    .Select(oi => oi.Service?.Subservice?.Category?.Name)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct()
                    .ToList();

                var orderName = string.Join(", ", categoryNames);
                var serviceCount = order.Orderitems.Count;

                var userOrderResponse = new UserOrderResponse
                {
                    OrderId = order.Orderid,
                    OrderName = orderName,
                    ServiceCount = serviceCount,
                    TotalPrice = order.Totalprice,
                    // Sử dụng hàm convert sang VN time, 
                    // hoặc tuỳ code base => .AddHours(7)
                    OrderedDate = _util.ConvertToVnTime(order.Createdat ?? DateTime.UtcNow),
                    OrderStatus = order.Currentstatus
                };

                resultList.Add(userOrderResponse);
            }

            // 5) Đóng gói pagination
            var paginationResult = new PaginationResult<UserOrderResponse>(
                data: resultList,
                totalRecords: totalRecords,
                currentPage: page,
                pageSize: pageSize
            );

            return paginationResult;
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

                PickupTime = order.Pickuptime,
                DeliveryTime = order.Deliverytime,
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
                        ExtraName = extraName,
                        ExtraPrice = extraPrice
                    });
                }

                var subTotal = (servicePrice + sumExtras) * item.Quantity;
                estimatedTotal += subTotal;

                orderSummary.Items.Add(new OrderItemSummary
                {
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

        public async Task<PaginationResult<InCartOrderAdminResponse>> GetInCartOrdersPagedAsync(HttpContext httpContext, int page, int pageSize)
        {
            // 1) Query order với status = "INCART"
            var query = _unitOfWork.Repository<Order>()
                .GetAll()
                .Where(o => o.Currentstatus == "INCART")
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Service)
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Orderextras)
                    .ThenInclude(oe => oe.Extra)
                .Include(o => o.User)
                .OrderBy(o => o.Createdat);

            // 3) Tính tổng số bản ghi
            var totalRecords = query.Count();

            // 4) Lấy dữ liệu trang
            var orders = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 5) Map sang InCartOrderAdminResponse
            var resultList = new List<InCartOrderAdminResponse>();

            foreach (var order in orders)
            {
                decimal estimatedTotal = 0;
                var itemList = new List<InCartOrderItemResponse>();

                foreach (var oi in order.Orderitems)
                {
                    var serviceName = oi.Service?.Name ?? "Unknown";
                    var servicePrice = oi.Service?.Price ?? 0;

                    decimal sumExtras = 0;
                    var extrasResponse = new List<InCartExtraResponse>();

                    foreach (var oe in oi.Orderextras)
                    {
                        var extraPrice = oe.Extra?.Price ?? 0;
                        extrasResponse.Add(new InCartExtraResponse
                        {
                            ExtraId = oe.Extraid,
                            ExtraName = oe.Extra?.Name ?? "Unknown Extra",
                            ExtraPrice = extraPrice
                        });
                        sumExtras += extraPrice;
                    }

                    var subTotal = (servicePrice + sumExtras) * oi.Quantity;
                    estimatedTotal += subTotal;

                    itemList.Add(new InCartOrderItemResponse
                    {
                        OrderItemId = oi.Orderitemid,
                        ServiceId = oi.Serviceid,
                        ServiceName = serviceName,
                        ServicePrice = servicePrice,
                        Quantity = oi.Quantity,
                        Extras = extrasResponse,
                        SubTotal = subTotal
                    });
                }

                var inCartResp = new InCartOrderAdminResponse
                {
                    OrderId = order.Orderid,
                    Items = itemList,
                    EstimatedTotal = estimatedTotal,
                    UserInfo = new AdminUserInfo
                    {
                        UserId = order.Userid,
                        FullName = order.User?.Fullname,
                        PhoneNumber = order.User?.Phonenumber
                    }
                };

                resultList.Add(inCartResp);
            }

            // 6) Tạo PaginationResult
            var paginationResult = new PaginationResult<InCartOrderAdminResponse>(
                data: resultList,
                totalRecords: totalRecords,
                currentPage: page,
                pageSize: pageSize
            );

            return paginationResult;
        }

        public async Task<Guid> ProcessOrderAsync(HttpContext httpContext, string orderId)
        {
            // 1) Bắt đầu transaction
            await _unitOfWork.BeginTransaction();

            try
            {
                // 2) Lấy order theo orderId
                var order = _unitOfWork.Repository<Order>()
                    .GetAll()
                    .Include(o => o.Orderassignmenthistories) // Lấy luôn assignment
                    .FirstOrDefault(o => o.Orderid == orderId);

                if (order == null)
                    throw new KeyNotFoundException("Order not found.");

                // 3) Kiểm tra status order PENDING
                if (order.Currentstatus != "PENDING")
                    throw new ApplicationException("Có lỗi xảy ra! Đơn hàng không ở trạng thái PENDING.");

                // 4) Kiểm tra xem trong Orderassignmenthistory
                //    có row status="processing" => Lỗi
                bool isProcessing = order.Orderassignmenthistories
                    .Any(ah => ah.Status == "PROCESSING");

                if (isProcessing)
                    throw new ApplicationException("Có lỗi xảy ra! Lỗi này thường do đơn hàng đang được xử lý bởi một người khác.");

                // 5) Lấy userId từ JWT
                var userId = _util.GetCurrentUserIdOrThrow(httpContext);

                // 6) Tạo Orderassignmenthistory
                var newAssignment = new Orderassignmenthistory
                {
                    Orderid = orderId,
                    Assignedto = userId,  // staff/driver ID
                    Assignedat = DateTime.UtcNow,
                    Status = "PROCESSING"
                };

                await _unitOfWork.Repository<Orderassignmenthistory>().InsertAsync(newAssignment, saveChanges: false);

                // 7) Lưu & commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();

                // 8) Sau khi commit thành công, return Assignmentid
                return newAssignment.Assignmentid;
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task ConfirmOrderAsync(HttpContext httpContext, string orderId, string notes)
        {
            // Bắt đầu transaction
            await _unitOfWork.BeginTransaction();

            try
            {
                var order = _unitOfWork.Repository<Order>()
                    .GetAll()
                    .FirstOrDefault(o => o.Orderid == orderId);

                if (order == null)
                {
                    throw new KeyNotFoundException("Order not found.");
                }

                //Chỉ xác nhận đơn có status = "PENDING"
                if (order.Currentstatus != OrderStatusEnum.PENDING.ToString())
                {
                    throw new ApplicationException(
                        $"Order {orderId} is not in PENDING status. Current: {order.Currentstatus}"
                    );
                }

                // 1) Tìm Orderassignmenthistory có orderId = orderId AND Status="processing"
                //    => ta giả định chỉ có 1 assignment "processing" tại 1 thời điểm
                var assignment = _unitOfWork.Repository<Orderassignmenthistory>()
                    .GetAll()
                    .FirstOrDefault(a => a.Orderid == orderId && a.Status == "PROCESSING");

                if (assignment == null)
                {
                    // Nếu không tìm thấy => báo lỗi
                    throw new ApplicationException("Không thể xác nhận đơn hàng. Lỗi: Không tìm thấy processing assignment.");
                }

                // 2) Cập nhật assignment => Status="SUCCESS", Completedat=UTCNow
                assignment.Status = "SUCCESS";
                assignment.Completedat = DateTime.UtcNow;
                await _unitOfWork.Repository<Orderassignmenthistory>().UpdateAsync(assignment, saveChanges: false);

                // 3) Tìm Order => set Currentstatus="CONFIRMED"
                // Cập nhật Currentstatus = "CONFIRMED"
                order.Currentstatus = "CONFIRMED";
                await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);

                // 4) Thêm record vào Orderstatushistory => status=CONFIRMED
                var userId = _util.GetCurrentUserIdOrThrow(httpContext);

                var newHistory = new Orderstatushistory
                {
                    Orderid = orderId,
                    Status = "CONFIRMED",
                    Statusdescription = "Đơn hàng đã được xác nhận",
                    Notes = notes,
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };

                await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(newHistory, saveChanges: false);

                // 5) Lưu & commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task CancelOrderAsync(HttpContext httpContext, Guid assignmentId, string notes)
        {
            // 1) Bắt đầu transaction
            await _unitOfWork.BeginTransaction();

            try
            {
                // 2) Tìm OrderAssignmentHistory bằng assignmentId
                var assignment = _unitOfWork.Repository<Orderassignmenthistory>()
                    .GetAll()
                    .Include(a => a.Order) // Eager load để ta truy cập Order, OrderID
                    .FirstOrDefault(a => a.Assignmentid == assignmentId);

                if (assignment == null)
                    throw new KeyNotFoundException("Không tìm thấy assignmentId này.");

                // 3) Kiểm tra thời gian, so sánh (UTCNow - Assignedat) <= 30p ?
                if (!assignment.Assignedat.HasValue)
                {
                    // Không có AssignedAt => logic sai
                    throw new ApplicationException("Assignment chưa có thông tin AssignedAt.");
                }

                var elapsed = DateTime.UtcNow - assignment.Assignedat.Value;
                if (elapsed > TimeSpan.FromMinutes(30))
                {
                    // Quá 30p => báo lỗi
                    throw new ApplicationException("Quá thời gian xử lý đơn hàng. Không thể hủy.");
                }

                // 4) Cập nhật assignment
                //    Status = SUCCESS (coi như staff kết thúc assignment),
                //    Completedat = utcnow
                assignment.Status = "SUCCESS";
                assignment.Completedat = DateTime.UtcNow;
                await _unitOfWork.Repository<Orderassignmenthistory>().UpdateAsync(assignment, saveChanges: false);

                // 5) Tạo row mới trong OrderStatusHistory => CANCELLED
                //    => "Đơn hàng đã hủy."
                //    => notes
                //    => updatedBy = userId
                var userId = _util.GetCurrentUserIdOrThrow(httpContext);
                var orderId = assignment.Orderid; // Từ assignment

                var cancelStatusHistory = new Orderstatushistory
                {
                    Orderid = orderId,
                    Status = "CANCELLED",
                    Statusdescription = "Đơn hàng đã hủy.",
                    Notes = notes,
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(cancelStatusHistory, saveChanges: false);

                // Set Order.Currentstatus="CANCELLED" để đồng bộ)
                var order = assignment.Order; // do ta Include(a => a.Order)
                if (order != null)
                {
                    order.Currentstatus = "CANCELLED";
                    await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);
                }

                // 6) Lưu và commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task CancelProcessingAsync(HttpContext httpContext, Guid assignmentId, string note)
        {
            // 1) Bắt đầu transaction
            await _unitOfWork.BeginTransaction();

            try
            {
                // 2) Tìm Orderassignmenthistory bằng assignmentId
                var assignment = _unitOfWork.Repository<Orderassignmenthistory>()
                    .GetAll()
                    .FirstOrDefault(a => a.Assignmentid == assignmentId);

                if (assignment == null)
                    throw new KeyNotFoundException("Không tìm thấy assignmentId này.");

                // 3) Kiểm tra thời gian
                if (!assignment.Assignedat.HasValue)
                {
                    throw new ApplicationException("Assignment chưa có AssignedAt nên không thể hủy xử lý.");
                }

                var elapsed = DateTime.UtcNow - assignment.Assignedat.Value;
                if (elapsed > TimeSpan.FromMinutes(30))
                {
                    // Quá 30p => báo lỗi
                    throw new ApplicationException("Quá thời gian xử lý đơn hàng. Không thể hủy.");
                }

                // 4) Cập nhật assignment => staff thoát xử lý
                assignment.Status = "SUCCESS";  // Hoặc "DONE", tùy logic
                assignment.Declinereason = note; // Lưu ghi chú/note
                assignment.Completedat = DateTime.UtcNow;

                await _unitOfWork.Repository<Orderassignmenthistory>().UpdateAsync(assignment, saveChanges: false);

                // 5) Lưu & commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
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

        public async Task<List<AreaOrdersResponse>> GetConfirmedOrdersByAreaAsync()
        {
            // B1: Lấy danh sách Orders có status = "CONFIRMED"
            var orders = _unitOfWork.Repository<Order>()
                .GetAll()
                .Where(o => o.Currentstatus == "CONFIRMED")
                .Include(o => o.User) // để lấy UserInfo
                .OrderBy(o => o.Createdat) // sắp xếp theo CreatedAt
                .ToList();
            // B2: Đọc config "Areas" từ appsettings
            //     "Areas": {
            //        "Area1": [ "1", "3", "4", "Tân Bình", ... ],
            //        "Area2": [ ... ],
            //        "Area3": [ ... ]
            //     }
            var area1 = _configuration.GetSection("Areas:Area1").Get<string[]>() ?? Array.Empty<string>();
            var area2 = _configuration.GetSection("Areas:Area2").Get<string[]>() ?? Array.Empty<string>();
            var area3 = _configuration.GetSection("Areas:Area3").Get<string[]>() ?? Array.Empty<string>();

            // B3: Lấy toạ độ "AddressDetail" từ appsettings để tính distance
            //    "AddressDetail": {
            //      "Address": "...",
            //      "Latitude": 10.809939,
            //      "Longitude": 106.664737
            //    }
            var addressSection = _configuration.GetSection("AddressDetail");
            decimal refLat = addressSection.GetValue<decimal>("Latitude");
            decimal refLon = addressSection.GetValue<decimal>("Longitude");

            // Tạo dictionary { "Area1": new List<ConfirmedOrderInfo>(), "Area2":..., "Area3":...}
            var areaDict = new Dictionary<string, List<ConfirmedOrderInfo>>()
            {
                { "Area1", new List<ConfirmedOrderInfo>() },
                { "Area2", new List<ConfirmedOrderInfo>() },
                { "Area3", new List<ConfirmedOrderInfo>() },
                // Trường hợp quận không nằm trong 3 area => tuỳ bạn, có thể cho "Outside" hay "Unknown"
            };

            // B4: Duyệt qua Orders -> gọi mapboxService để lấy district
            foreach (var order in orders)
            {
                var pickupLat = order.Pickuplatitude ?? 0;
                var pickupLon = order.Pickuplongitude ?? 0;

                // Gọi mapboxService để lấy tên quận
                var district = await _mapboxService.GetDistrictFromCoordinatesAsync(pickupLat, pickupLon);
                district = district?.Trim() ?? ""; // để phòng null

                // Xác định area
                var areaName = "Unknown";
                // Kiểm tra district có nằm trong "Area1" ?
                if (area1.Contains(district, StringComparer.OrdinalIgnoreCase))
                    areaName = "Area1";
                else if (area2.Contains(district, StringComparer.OrdinalIgnoreCase))
                    areaName = "Area2";
                else if (area3.Contains(district, StringComparer.OrdinalIgnoreCase))
                    areaName = "Area3";

                // Tính distance so với lat/lon trong appsettings
                double distance = _mapboxService.CalculateDistance(pickupLat, pickupLon, refLat, refLon);

                // Tạo object ConfirmedOrderInfo
                var orderInfo = new ConfirmedOrderInfo
                {
                    OrderId = order.Orderid,
                    UserInfo = new UserInfoDto
                    {
                        UserId = order.Userid,
                        FullName = order.User?.Fullname,
                        PhoneNumber = order.User?.Phonenumber
                    },
                    PickupName = order.Pickupname,
                    PickupPhone = order.Pickupphone,
                    Distance = distance,
                    PickupAddressDetail = order.Pickupaddressdetail,
                    PickupDescription = order.Pickupdescription,
                    PickupLatitude = pickupLat,
                    PickupLongitude = pickupLon,
                    PickupTime = order.Pickuptime,
                    CreatedAt = order.Createdat ?? DateTime.UtcNow,
                    TotalPrice = order.Totalprice
                };

                // Thêm vào list tương ứng
                if (!areaDict.ContainsKey(areaName))
                {
                    areaDict[areaName] = new List<ConfirmedOrderInfo>();
                }
                areaDict[areaName].Add(orderInfo);
            }

            // B5: Tạo list kết quả -> group theo Area
            // Mỗi key trong areaDict -> 1 AreaOrdersResponse
            // Sắp xếp trong cùng 1 khu vực theo CreatedAt
            var result = new List<AreaOrdersResponse>();
            foreach (var kv in areaDict)
            {
                // Sắp xếp kv.Value (danh sách ConfirmedOrderInfo) theo CreatedAt
                var sortedOrders = kv.Value
                    .OrderBy(o => o.CreatedAt)
                    .ToList();

                // Bỏ qua group rỗng? Tùy: 
                // - Nếu muốn hiển thị group rỗng => vẫn add
                // - Nếu không => check if (sortedOrders.Any()) ...
                if (sortedOrders.Any())
                {
                    result.Add(new AreaOrdersResponse
                    {
                        Area = kv.Key,
                        Orders = sortedOrders
                    });
                }
            }

            return result;
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

        public async Task StartOrderPickupAsync(HttpContext httpContext, string orderId)
        {
            await _unitOfWork.BeginTransaction();
            try
            {
                var assignment = _unitOfWork.Repository<Orderassignmenthistory>()
                    .GetAll()
                    .FirstOrDefault(a => a.Orderid == orderId && a.Status == "ASSIGNED_PICKUP");

                if (assignment == null)
                    throw new ApplicationException("Không tìm thấy assignment có trạng thái ASSIGNED_PICKUP.");

                assignment.Status = "PICKING_UP";
                await _unitOfWork.Repository<Orderassignmenthistory>().UpdateAsync(assignment, saveChanges: false);

                var order = _unitOfWork.Repository<Order>()
                    .GetAll()
                    .FirstOrDefault(o => o.Orderid == orderId);
                if (order == null)
                    throw new KeyNotFoundException("Order not found.");

                order.Currentstatus = "PICKING_UP";
                await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);


                var userId = _util.GetCurrentUserIdOrThrow(httpContext);
                var history = new Orderstatushistory
                {
                    Orderid = orderId,
                    Status = "PICKING_UP",
                    Statusdescription = "Tài xế đang tiến hành đi nhận hàng",
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow

                };
                await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(history, saveChanges: false);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task ConfirmOrderPickedUpAsync(HttpContext httpContext, string orderId, string notes)
        {
            await _unitOfWork.BeginTransaction();
            try
            {
                var assignment = _unitOfWork.Repository<Orderassignmenthistory>()
                    .GetAll()
                    .FirstOrDefault(a => a.Orderid == orderId && a.Status == "PICKING_UP");

                if (assignment == null)
                    throw new ApplicationException("Không tìm thấy assignment với trạng thái PICKING_UP.");

                assignment.Status = "PICKED_UP";
                await _unitOfWork.Repository<Orderassignmenthistory>().UpdateAsync(assignment, saveChanges: false);

                var order = _unitOfWork.Repository<Order>()
                    .GetAll()
                    .FirstOrDefault(o => o.Orderid == orderId);
                if (order == null)
                    throw new KeyNotFoundException("Order not found.");

                order.Currentstatus = "PICKED_UP";
                await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);

                var userId = _util.GetCurrentUserIdOrThrow(httpContext);
                var history = new Orderstatushistory
                {
                    Orderid = orderId,
                    Status = "PICKED_UP",
                    Statusdescription = "Tài xế đã nhận hàng thành công",
                    Notes = notes,
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(history, saveChanges: false);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task ConfirmOrderReceivedAsync(HttpContext httpContext, string orderId)
        {
            await _unitOfWork.BeginTransaction();
            try
            {
                var assignment = _unitOfWork.Repository<Orderassignmenthistory>()
                    .GetAll()
                    .FirstOrDefault(a => a.Orderid == orderId && a.Status == "PICKED_UP");

                if (assignment == null)
                    throw new ApplicationException("Không tìm thấy assignment với trạng thái PICKED_UP.");

                assignment.Status = "RECEIVED";
                assignment.Completedat = DateTime.UtcNow;
                await _unitOfWork.Repository<Orderassignmenthistory>().UpdateAsync(assignment, saveChanges: false);

                var order = _unitOfWork.Repository<Order>()
                    .GetAll()
                    .FirstOrDefault(o => o.Orderid == orderId);
                if (order == null)
                    throw new KeyNotFoundException("Order not found.");

                order.Currentstatus = "CHECKING";
                await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);

                var userId = _util.GetCurrentUserIdOrThrow(httpContext);
                var history = new Orderstatushistory
                {
                    Orderid = orderId,
                    Status = "RECEIVED",
                    Statusdescription = "Tài xế đã nhận hàng về tới nơi",
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(history, saveChanges: false);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task StartOrderDeliveryAsync(HttpContext httpContext, string orderId)
        {
            await _unitOfWork.BeginTransaction();
            try
            {
                var assignment = _unitOfWork.Repository<Orderassignmenthistory>()
                    .GetAll()
                    .FirstOrDefault(a => a.Orderid == orderId && a.Status == "ASSIGNED_DELIVERY");

                if (assignment == null)
                    throw new ApplicationException("Không tìm thấy assignment có trạng thái ASSIGNED_DELIVERY.");

                assignment.Status = "DELIVERING";
                await _unitOfWork.Repository<Orderassignmenthistory>().UpdateAsync(assignment, saveChanges: false);

                var order = _unitOfWork.Repository<Order>()
                    .GetAll()
                    .FirstOrDefault(o => o.Orderid == orderId);
                if (order == null)
                    throw new KeyNotFoundException("Order not found.");

                order.Currentstatus = "DELIVERING";
                await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);

                var userId = _util.GetCurrentUserIdOrThrow(httpContext);
                var history = new Orderstatushistory
                {
                    Orderid = orderId,
                    Status = "DELIVERING",
                    Statusdescription = "Tài xế đang giao hàng",
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(history, saveChanges: false);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task ConfirmOrderDeliveredAsync(HttpContext httpContext, string orderId, string notes)
        {
            await _unitOfWork.BeginTransaction();
            try
            {
                var assignment = _unitOfWork.Repository<Orderassignmenthistory>()
                    .GetAll()
                    .FirstOrDefault(a => a.Orderid == orderId && a.Status == "DELIVERING");

                if (assignment == null)
                    throw new ApplicationException("Không tìm thấy assignment với trạng thái DELIVERING.");

                assignment.Status = "DELIVERED";
                assignment.Completedat = DateTime.UtcNow;
                await _unitOfWork.Repository<Orderassignmenthistory>().UpdateAsync(assignment, saveChanges: false);

                var order = _unitOfWork.Repository<Order>()
                    .GetAll()
                    .FirstOrDefault(o => o.Orderid == orderId);
                if (order == null)
                    throw new KeyNotFoundException("Order not found.");

                order.Currentstatus = "DELIVERED";
                await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);

                var userId = _util.GetCurrentUserIdOrThrow(httpContext);
                var history = new Orderstatushistory
                {
                    Orderid = orderId,
                    Status = "DELIVERED",
                    Statusdescription = "Tài xế đã giao hàng thành công",
                    Notes = notes,
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(history, saveChanges: false);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task AssignPickupToDriverAsync(HttpContext httpContext, AssignPickupRequest request)
        {
            // 1) Lấy userId admin từ token (người gọi API)
            var adminUserId = _util.GetCurrentUserIdOrThrow(httpContext);

            // 2) Validate request
            if (request.DriverId == Guid.Empty)
                throw new ArgumentException("DriverId is required.");
            if (request.OrderIds == null || request.OrderIds.Count == 0)
                throw new ArgumentException("OrderIds cannot be empty.");

            // Kiểm tra DriverId có tồn tại và có Role "Driver" không
            var driver = await _unitOfWork.Repository<User>()
                                        .GetAsync(u => u.Userid == request.DriverId && u.Role == "Driver");
            if (driver == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy Driver với ID: {request.DriverId} hoặc người dùng này không phải Driver.");
            }

            // 3) Bắt đầu transaction
            await _unitOfWork.BeginTransaction();
            try
            {
                // 4) Duyệt từng orderId
                foreach (var orderId in request.OrderIds)
                {
                    // a) Tìm Order => validate
                    var order = _unitOfWork.Repository<Order>()
                        .GetAll()
                        .FirstOrDefault(o => o.Orderid == orderId);

                    if (order == null)
                    {
                        throw new KeyNotFoundException($"OrderId {orderId} not found.");
                    }

                    // Chỉ assign pickup nếu order đang CONFIRMED
                    if (order.Currentstatus != OrderStatusEnum.CONFIRMED.ToString())
                    {
                        throw new ApplicationException(
                            $"Order {orderId} is not in CONFIRMED status. Current: {order.Currentstatus}"
                        );
                    }

                    // b) Tạo record Orderassignmenthistory
                    var assignment = new Orderassignmenthistory
                    {
                        Orderid = orderId,
                        Assignedto = request.DriverId,
                        Assignedat = DateTime.UtcNow,
                        Status = AssignStatusEnum.ASSIGNED_PICKUP.ToString(), // từ enum AssignStatusEnum
                    };
                    await _unitOfWork.Repository<Orderassignmenthistory>().InsertAsync(assignment, saveChanges: false);

                    // c) Tạo record Orderstatushistory
                    var statusHistory = new Orderstatushistory
                    {
                        Orderid = orderId,
                        Status = OrderStatusEnum.SCHEDULED_PICKUP.ToString(),
                        Statusdescription = "Đã lên lịch lấy hàng",
                        Updatedby = adminUserId,
                        Createdat = DateTime.UtcNow
                    };
                    await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(statusHistory, saveChanges: false);

                    // d) Cập nhật Order -> Currentstatus = "SCHEDULED_PICKUP"
                    order.Currentstatus = OrderStatusEnum.SCHEDULED_PICKUP.ToString();
                    await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);
                }

                // 5) Lưu và commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }
    }
}