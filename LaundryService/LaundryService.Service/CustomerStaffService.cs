using DocumentFormat.OpenXml.Spreadsheet;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Pagination;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class CustomerStaffService : ICustomerStaffService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUtil _util;
        private readonly IOrderService _orderService;
        private readonly IAddressService _addressService;
        private readonly IMapboxService _mapboxService;

        public CustomerStaffService(IUnitOfWork unitOfWork, IUtil util, IOrderService orderService, IAddressService addressService, IMapboxService mapboxService)
        {
            _unitOfWork = unitOfWork;
            _util = util;
            _orderService = orderService;
            _addressService = addressService;
            _mapboxService = mapboxService;
        }

        public async Task<PaginationResult<PendingOrdersResponse>> GetPendingOrdersForStaffAsync(HttpContext httpContext, int page, int pageSize)
        {
            var currentStaffId = _util.GetCurrentUserIdOrThrow(httpContext);

            // 1) Tạo query
            var queryOrders = _unitOfWork.Repository<Order>()
                        .GetAll()
                        .Where(o => o.Currentstatus == "PENDING")      // chỉ PENDING
                        /* loại bỏ đơn đang PROCESSING bởi NGƯỜI KHÁC */
                        .Where(o => !o.Orderassignmenthistories
                                        .Any(ah => ah.Status == "PROCESSING" &&
                                                   ah.Assignedto != currentStaffId))
                        .Include(o => o.Orderassignmenthistories)
                        .Include(o => o.Orderitems)
                            .ThenInclude(oi => oi.Service)
                                .ThenInclude(s => s.Subservice)
                                    .ThenInclude(sb => sb.Category)
                        .OrderByDescending(o => o.Createdat);

            /* 2) Phân trang */
            var totalRecords = queryOrders.Count();

            var orders = queryOrders.Skip((page - 1) * pageSize)
                                  .Take(pageSize)
                                  .ToList();

            /* 3) Map → DTO */
            var list = orders.Select(o =>
            {
                var categories = o.Orderitems
                                  .Select(oi => oi.Service?.Subservice?.Category?.Name)
                                  .Where(n => !string.IsNullOrEmpty(n))
                                  .Distinct();

                return new PendingOrdersResponse
                {
                    OrderId = o.Orderid,
                    OrderName = string.Join(", ", categories),
                    ServiceCount = o.Orderitems.Count,
                    TotalPrice = o.Totalprice,
                    OrderedDate = _util.ConvertToVnTime(o.Createdat ?? DateTime.UtcNow),
                    OrderStatus = o.Currentstatus,
                    Emergency = o.Emergency,
                    AssignmentId = o.Orderassignmenthistories.FirstOrDefault(aah => aah.Status == "PROCESSING" && aah.Assignedto == currentStaffId)?.Assignmentid
                };
            }).ToList();

            return new PaginationResult<PendingOrdersResponse>(
                data: list,
                totalRecords: totalRecords,
                currentPage: page,
                pageSize: pageSize);
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
                    throw new ApplicationException("Có lỗi xảy ra! Đơn hàng này đã ở trạng thái PROCESSING");

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

        /// <summary>CustomerStaff thêm sản phẩm vào giỏ hàng </summary>
        public async Task StaffAddToCartAsync(Guid userId, AddToCartRequest request)
        {
            await _unitOfWork.BeginTransaction();
            try
            {
                await _orderService.AddToCartNoTransactionAsync(userId, request);
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
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

        /// <summary>CustomerStaff thêm item vào Order.</summary>
        public async Task AddItemToOrderAsync(string orderId, AddToCartRequest request)
        {
            await _unitOfWork.BeginTransaction();
            try
            {
                // 1) Kiểm tra ServiceDetail
                var serviceDetail = await _unitOfWork
                    .Repository<Servicedetail>()
                    .GetAsync(s => s.Serviceid == request.ServiceDetailId);

                if (serviceDetail == null)
                    throw new KeyNotFoundException("Service detail not found.");

                var order = _unitOfWork.Repository<Order>().GetAll().FirstOrDefault(o => o.Orderid == orderId);

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

                decimal newItemBasePrice = 0;
                decimal newItemExtraPrice = 0;

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
                    newItemBasePrice = oi.Baseprice ?? 0;

                    // Lấy list OrderExtra gắn với OrderItem này
                    var oiExtras = _unitOfWork.Repository<Orderextra>()
                        .GetAll()
                        .Where(e => e.Orderitemid == oi.Orderitemid)
                        .ToList();

                    newItemExtraPrice = oiExtras.Sum(e => e.Extraprice ?? 0);

                    // So sánh
                    if (ExtrasAreTheSame(oiExtras, request.ExtraIds ?? new List<Guid>()))
                    {
                        matchedItem = oi;
                        break;
                    }
                    else
                    {
                        newItemBasePrice = 0;
                        newItemExtraPrice = 0;
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
                    newItemBasePrice = serviceDetail.Price; // Giá cơ bản của ServiceDetail

                    // Ngược lại => tạo OrderItem mới
                    var newOrderItem = new Orderitem
                    {
                        Orderid = order.Orderid,
                        Serviceid = serviceDetail.Serviceid,
                        Quantity = request.Quantity,
                        Baseprice = serviceDetail.Price,
                        Createdat = DateTime.UtcNow
                    };
                    await _unitOfWork.Repository<Orderitem>().InsertAsync(newOrderItem, saveChanges: false);
                    await _unitOfWork.SaveChangesAsync();

                    // Nếu có Extras => Insert OrderExtra
                    if (validExtras.Any())
                    {
                        var newOrderExtras = validExtras.Select(ext => new Orderextra
                        {
                            Orderitemid = newOrderItem.Orderitemid,
                            Extraid = ext.Extraid,
                            Extraprice = ext.Price,
                            Createdat = DateTime.UtcNow
                        }).ToList();

                        newItemExtraPrice = newOrderExtras.Sum(e => e.Extraprice ?? 0);

                        await _unitOfWork.Repository<Orderextra>().InsertRangeAsync(newOrderExtras, saveChanges: false);
                    }
                }
                order.Totalprice += (newItemBasePrice + newItemExtraPrice) * request.Quantity;
                await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);

                await _unitOfWork.SaveChangesAsync();

                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        // Cập nhật item trong Order
        public async Task UpdateItemInOrderAsync(UpdateCartItemRequest request)
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

                if (orderItem.Order.Currentstatus != "PENDING" && orderItem.Order.Currentstatus != "CHECKING")
                    throw new ApplicationException("Không thể edit item  khi order không ở trạng thái PENDING hoặc CHECKING.");

                var order = orderItem.Order;
            
                // 2) Xóa OrderExtra liên quan đến item này
                var oldOrderExtras = _unitOfWork.Repository<Orderextra>()
                    .GetAll()
                    .Where(e => e.Orderitemid == orderItem.Orderitemid)
                    .ToList();

                // Tổng giá cũ của item
                decimal oldItemTotalPrice = (orderItem.Baseprice ?? 0) * orderItem.Quantity + oldOrderExtras.Sum(e => e.Extraprice ?? 0);

                // Cập nhật giá TotalPrice của Order
                order.Totalprice -= oldItemTotalPrice;
                await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);
                await _unitOfWork.SaveChangesAsync();

                if (oldOrderExtras.Any())
                {
                    await _unitOfWork.Repository<Orderextra>().DeleteRangeAsync(oldOrderExtras, saveChanges: false);
                }

                // xóa luôn chính OrderItem này
                await _unitOfWork.Repository<Orderitem>().DeleteAsync(orderItem, saveChanges: false);
                await _unitOfWork.SaveChangesAsync();

                // Nếu quantity = 0 thì không cần thêm lại
                // kiểm tra xem order có còn item nào không, nếu không thì báo lỗi không thể cập nhật
                if (request.Quantity == 0)
                {
                    var remainingItems = _unitOfWork.Repository<Orderitem>()
                        .GetAll()
                        .Where(oi => oi.Orderid == order.Orderid)
                        .Count();

                    if (remainingItems == 0)
                    {
                        throw new ApplicationException("Cannot update item because order has no items left.");
                    }
                }
                else
                {
                    // Nếu quantity > 0 thì thêm lại Item mới
                    var addRequest = new AddToCartRequest
                    {
                        ServiceDetailId = orderItem.Serviceid,
                        Quantity = request.Quantity,
                        ExtraIds = request.ExtraIds
                    };
                    await AddItemToOrderAsync(order.Orderid, addRequest);
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

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

        // Tính phí shipping cho đơn hàng
        public async Task<CalculateShippingFeeResponse> CalculateShippingFeeAsync(CusStaffCalculateShippingFeeRequest req)
        {
            decimal shippingFee = 0m; // Khởi tạo phí ship

            // Lấy giờ VN để khớp với nghiệp vụ (dùng hàm util sẵn có)
            var nowVn = DateTime.UtcNow.AddHours(7);

            var diffProcess = req.DeliveryTime - nowVn;
            if (diffProcess.TotalHours < req.MinCompleteTime)
            {
                var unit = req.MinCompleteTime >= 24 ? "ngày" : "giờ";
                var min = req.MinCompleteTime / (unit == "ngày" ? 24 : 1);
                throw new ApplicationException(
                    $"Thời gian xử lý không đủ vì món đồ {req.ServiceName} có thời gian xử lý tối thiểu là {min} {unit}.");
            }

            // Nếu address delivery không null, thì tính phí ship
            if (req.DeliveryAddressId.HasValue && req.DeliveryAddressId.Value != Guid.Empty)
            {
                // Kiểm tra địa chỉ pickup/delivery có tồn tại không
                var deliveryAddr = await _unitOfWork.Repository<Address>().FindAsync(req.DeliveryAddressId)
                                 ?? throw new KeyNotFoundException("Delivery address not found.");

                /* ---------- 2. Lấy tọa độ & District ---------- */
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

                /* -------- 4. Tính phí ---------- */
                shippingFee = GetLegShippingFee(districtToArea, deliveryDistrict, shippingAreas);

                /* -------- 5. Giảm phí theo EstimatedTotal ---------- */
                if (req.EstimatedTotal >= 1_000_000m) shippingFee = 0;
                else if (req.EstimatedTotal >= 350_000m) shippingFee *= 0.5m;
            }

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

        public async Task<string> CusStaffPlaceOrderAsync(HttpContext httpContext, Guid userId, CusStaffPlaceOrderRequest request)
        {
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

                // 2) Lấy thông tin Address delivery nếu có
                if (!string.IsNullOrEmpty(request.DeliveryAddressId.ToString()))
                {
                    var deliveryAddress = _unitOfWork.Repository<Address>()
                        .GetAll()
                        .FirstOrDefault(a => a.Addressid == request.DeliveryAddressId && a.Userid == userId);
                    if (deliveryAddress == null)
                    {
                        throw new KeyNotFoundException("Delivery address not found for current user.");
                    }

                    order.Deliverylabel = deliveryAddress.Addresslabel;
                    order.Deliveryname = deliveryAddress.Contactname;
                    order.Deliveryphone = deliveryAddress.Contactphone;
                    order.Deliveryaddressdetail = deliveryAddress.Detailaddress;
                    order.Deliverydescription = deliveryAddress.Description;
                    order.Deliverylatitude = deliveryAddress.Latitude;
                    order.Deliverylongitude = deliveryAddress.Longitude;
                }

                // 4) Gán thời gian delivery
                if (!string.IsNullOrEmpty(request.Deliverytime.ToString()))
                {
                    DateTime deliveryTime = request.Deliverytime.Value.ToUniversalTime(); // Chuyển sang UTC
                    order.Deliverytime = deliveryTime;
                }

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
                //    total = basePriceSum + shippingFee + applicableFee + discount
                decimal shippingFee = request.Shippingfee ?? 0;
                decimal applicableFee = request.Applicablefee ?? 0;
                decimal discount = request.Discount ?? 0;

                decimal finalTotal = basePriceSum
                                    + shippingFee
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

                // Nếu có Applicablefee thì Emergency là true
                if (applicableFee > 0)
                {
                    order.Emergency = true;
                }
                else
                {
                    order.Emergency = false;
                }

                // 7) Gán các trường còn lại vào Order
                order.Shippingfee = shippingFee;
                order.Applicablefee = applicableFee;
                order.Discount = discount;
                order.Totalprice = finalTotal;
                order.Currentstatus = "CHECKING";
                order.Createdat = DateTime.UtcNow; //thay đổi ngày tạo thành ngày place order

                // Update Order
                await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);

                // 8) Tạo OrderStatusHistory: "CHECKING"
                var newStatusHistory = new Orderstatushistory
                {
                    Orderid = order.Orderid,
                    Status = "CHECKING",
                    Statusdescription = "Đơn hàng đang được kiểm tra.",
                    Notes = request.Note,
                    Updatedby = _util.GetCurrentUserIdOrThrow(httpContext),
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

        // CustomerStaff tạo địa chỉ cho customer
        public async Task<AddressResponse> CreateAddressAsync(Guid userId, CreateAddressRequest request)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("UserId không được để trống.", nameof(userId));
            }
            return await _addressService.CreateAddressAsync(userId, request);
        }

        public async Task AddOtherPrice(string orderId, AddOtherPriceRequest request)
        {
            if (string.IsNullOrWhiteSpace(orderId) || request == null)
                throw new ArgumentException("OrderId and request cannot be null or empty.");

            // 1) Lấy Order
            var order = await _unitOfWork.Repository<Order>().GetAll().FirstOrDefaultAsync(o => o.Orderid == orderId);

            if (order == null)
                throw new KeyNotFoundException("Order not found.");

            decimal deltaPrice = request.otherPrice - (order.Otherprice ?? 0);

            // 2) Cập nhật OtherPrice và Note
            order.Totalprice += deltaPrice;
            order.Otherprice = request.otherPrice;
            order.Noteforotherprice = request.otherPriceNote;
            // 3) Cập nhật Order
            await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: true);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
