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

        public CustomerStaffService(IUnitOfWork unitOfWork, IUtil util, IOrderService orderService)
        {
            _unitOfWork = unitOfWork;
            _util = util;
            _orderService = orderService;
        }

        /// <summary>
        /// Chỉ load các order có status = "PENDING"
        /// và KHÔNG có OrderAssignmentHistory nào có status = "processing"
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="page"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public async Task<PaginationResult<UserOrderResponse>> GetPendingOrdersForStaffAsync(HttpContext httpContext, int page, int pageSize)
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

                return new UserOrderResponse
                {
                    OrderId = o.Orderid,
                    OrderName = string.Join(", ", categories),
                    ServiceCount = o.Orderitems.Count,
                    TotalPrice = o.Totalprice,
                    OrderedDate = _util.ConvertToVnTime(o.Createdat ?? DateTime.UtcNow),
                    OrderStatus = o.Currentstatus
                };
            }).ToList();

            return new PaginationResult<UserOrderResponse>(
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
                decimal shippingFee = request.Shippingfee ?? 0;
                decimal shippingDiscount = request.Shippingdiscount ?? 0;
                decimal applicableFee = request.Applicablefee ?? 0;
                decimal discount = request.Discount ?? 0;

                decimal finalTotal = basePriceSum
                                    + shippingFee
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
                order.Shippingfee = shippingFee;
                order.Shippingdiscount = shippingDiscount;
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
    }
}
