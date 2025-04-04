using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Pagination;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using LaundryService.Infrastructure;
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
        private readonly IUtil _util;

        public OrderService(IUnitOfWork unitOfWork, IUtil util)
        {
            _unitOfWork = unitOfWork;
            _util = util;
        }

        public async Task AddToCartAsync(HttpContext httpContext, AddToCartRequest request)
        {
            // 1) Lấy userId từ token
            var userId = _util.GetCurrentUserIdOrThrow(httpContext);

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

        public async Task<Guid> PlaceOrderAsync(HttpContext httpContext, PlaceOrderRequest request)
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

        public async Task<OrderDetailCustomResponse> GetOrderDetailCustomAsync(HttpContext httpContext, Guid orderId)
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

        public async Task<List<OrderStatusHistoryItemResponse>> GetOrderStatusHistoryAsync(HttpContext httpContext, Guid orderId)
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

        public async Task<Guid> ProcessOrderAsync(HttpContext httpContext, Guid orderId)
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

        public async Task ConfirmOrderAsync(HttpContext httpContext, Guid orderId, string notes)
        {
            // Bắt đầu transaction
            await _unitOfWork.BeginTransaction();

            try
            {
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
                var order = _unitOfWork.Repository<Order>()
                    .GetAll()
                    .FirstOrDefault(o => o.Orderid == orderId);

                if (order == null)
                {
                    throw new KeyNotFoundException("Order not found.");
                }

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

    }
}