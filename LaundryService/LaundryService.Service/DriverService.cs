using LaundryService.Domain.Entities;
using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class DriverService : IDriverService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUtil _util;
        private readonly IFileStorageService _fileStorageService;
        private readonly IOrderJobService _jobService;

        public DriverService(IUnitOfWork unitOfWork, IUtil util, IFileStorageService fileStorageService, IOrderJobService jobService)
        {
            _unitOfWork = unitOfWork;
            _util = util;
            _fileStorageService = fileStorageService;
            _jobService = jobService;
        }

        public async Task StartOrderPickupAsync(HttpContext httpContext, string orderId)
        {
            await _unitOfWork.BeginTransaction();

            try
            {
                var statusAssignedPickup = AssignStatusEnum.ASSIGNED_PICKUP.ToString();
                var statusPickingUp = OrderStatusEnum.PICKINGUP.ToString();
                var orderRepo = _unitOfWork.Repository<Order>();
                var assignmentRepo = _unitOfWork.Repository<Orderassignmenthistory>();
                var statusHistoryRepo = _unitOfWork.Repository<Orderstatushistory>();
                var userId = _util.GetCurrentUserIdOrThrow(httpContext);

                var order = await orderRepo
                    .GetAll()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Orderid == orderId);
                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                var assignedTo = await assignmentRepo
                    .GetAll()
                    .Where(a => a.Orderid == orderId && a.Status == statusAssignedPickup)
                    .Select(a => a.Assignedto)
                    .FirstOrDefaultAsync();
                if (assignedTo == default)
                    throw new InvalidOperationException("Đơn hàng này hiện không có phân công hoặc trạng thái công việc không hợp lệ.");

                if (assignedTo != userId)
                    throw new UnauthorizedAccessException("Bạn không được giao thực hiện đơn này.");

                order.Currentstatus = statusPickingUp;
                await orderRepo.UpdateAsync(order, saveChanges: false);

                var history = new Orderstatushistory
                {
                    Orderid = orderId,
                    Status = statusPickingUp,
                    Statusdescription = "Tài xế đang tiến hành đi nhận hàng",
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };
                await statusHistoryRepo.InsertAsync(history, saveChanges: false);

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
                var statusAssignedPickup = AssignStatusEnum.ASSIGNED_PICKUP.ToString();
                var statusPickingUp = OrderStatusEnum.PICKINGUP.ToString();
                var statusPickedUp = OrderStatusEnum.PICKEDUP.ToString();
                var userId = _util.GetCurrentUserIdOrThrow(httpContext);
                var orderRepo = _unitOfWork.Repository<Order>();
                var assignmentRepo = _unitOfWork.Repository<Orderassignmenthistory>();
                var statusHistoryRepo = _unitOfWork.Repository<Orderstatushistory>();
                var photoRepo = _unitOfWork.Repository<Orderphoto>();

                var order = await orderRepo
                    .GetAll()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Orderid == orderId);
                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                var assignedTo = await assignmentRepo
                    .GetAll()
                    .Where(a => a.Orderid == orderId && a.Status == statusAssignedPickup)
                    .Select(a => a.Assignedto)
                    .FirstOrDefaultAsync();
                if (assignedTo == default)
                    throw new InvalidOperationException("Đơn hàng này chưa được phân công nhận hàng.");

                if (assignedTo != userId)
                    throw new UnauthorizedAccessException("Bạn không có quyền xác nhận đơn này.");

                if (order.Currentstatus != statusPickingUp)
                    throw new InvalidOperationException("Đơn hàng đang không ở bước này, vui lòng kiểm tra lại.");

                var files = httpContext.Request.Form.Files;
                if (files == null || files.Count == 0)
                    throw new ArgumentException("Vui lòng chụp và gửi ít nhất một ảnh khi nhận hàng.");

                order.Currentstatus = statusPickedUp;
                await orderRepo.UpdateAsync(order, saveChanges: false);

                var history = new Orderstatushistory
                {
                    Statushistoryid = Guid.NewGuid(),
                    Orderid = orderId,
                    Status = statusPickedUp,
                    Statusdescription = "Tài xế đã nhận hàng thành công",
                    Notes = notes,
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };
                await statusHistoryRepo.InsertAsync(history, saveChanges: false);

                var uploadTasks = files.Select(async file =>
                {
                    var url = await _fileStorageService.UploadFileAsync(file, "order-photos");
                    var photo = new Orderphoto
                    {
                        Photoid = Guid.NewGuid(),
                        Statushistoryid = history.Statushistoryid,
                        Photourl = url
                    };
                    await photoRepo.InsertAsync(photo, saveChanges: false);
                });
                await Task.WhenAll(uploadTasks);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task ConfirmOrderPickupSuccessAsync(HttpContext httpContext, string orderId)
        {
            await _unitOfWork.BeginTransaction();

            try
            {
                var statusPickedUp = OrderStatusEnum.PICKEDUP.ToString();
                var statusPickupSuccess = AssignStatusEnum.PICKUP_SUCCESS.ToString();
                var statusAssignedPickup = AssignStatusEnum.ASSIGNED_PICKUP.ToString();
                var userId = _util.GetCurrentUserIdOrThrow(httpContext);
                var orderRepo = _unitOfWork.Repository<Order>();
                var assignmentRepo = _unitOfWork.Repository<Orderassignmenthistory>();
                var statusHistoryRepo = _unitOfWork.Repository<Orderstatushistory>();

                var order = await orderRepo
                    .GetAll()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Orderid == orderId);
                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                var assignedTo = await assignmentRepo
                    .GetAll()
                    .Where(a => a.Orderid == orderId && a.Status == statusAssignedPickup)
                    .Select(a => a.Assignedto)
                    .FirstOrDefaultAsync();
                if (assignedTo == default)
                    throw new InvalidOperationException("Đơn hàng này chưa được phân công nhận hàng.");

                if (assignedTo != userId)
                    throw new UnauthorizedAccessException("Bạn không có quyền hoàn thành bước nhận hàng này.");

                if (order.Currentstatus != statusPickedUp)
                    throw new InvalidOperationException("Đơn hàng đang không ở bước này, vui lòng kiểm tra lại.");

                var assignment = await assignmentRepo
                    .GetAll()
                    .FirstOrDefaultAsync(a =>
                        a.Orderid == orderId &&
                        a.Status == statusAssignedPickup &&
                        a.Assignedto == userId);
                assignment.Status = statusPickupSuccess;
                assignment.Completedat = DateTime.UtcNow;
                await assignmentRepo.UpdateAsync(assignment, saveChanges: false);

                var history = new Orderstatushistory
                {
                    Orderid = orderId,
                    Status = statusPickupSuccess,
                    Statusdescription = "Tài xế đã nhận hàng về tới nơi",
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };
                await statusHistoryRepo.InsertAsync(history, saveChanges: false);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task CancelAssignedPickupAsync(HttpContext httpContext, string orderId, string cancelReason)
        {
            if (string.IsNullOrWhiteSpace(cancelReason))
                throw new ArgumentException("Lý do huỷ không được để trống.");

            await _unitOfWork.BeginTransaction();
            try
            {
                var statusAssigned = AssignStatusEnum.ASSIGNED_PICKUP.ToString();
                var statusPickupFailed = AssignStatusEnum.PICKUP_FAILED.ToString();
                var userId = _util.GetCurrentUserIdOrThrow(httpContext);
                var orderRepo = _unitOfWork.Repository<Order>();
                var assignmentRepo = _unitOfWork.Repository<Orderassignmenthistory>();

                var order = await orderRepo
                    .GetAll().AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Orderid == orderId);
                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                var assignedTo = await assignmentRepo
                    .GetAll()
                    .Where(a => a.Orderid == orderId && a.Status == statusAssigned)
                    .Select(a => a.Assignedto)
                    .FirstOrDefaultAsync();
                if (assignedTo == default)
                    throw new InvalidOperationException("Đơn này chưa được phân công nhận hàng.");

                if (assignedTo != userId)
                    throw new UnauthorizedAccessException("Bạn không có quyền huỷ nhận đơn này.");

                var assignment = await assignmentRepo
                    .GetAll()
                    .FirstOrDefaultAsync(a =>
                        a.Orderid == orderId &&
                        a.Status == statusAssigned &&
                        a.Assignedto == userId);

                assignment.Status = statusPickupFailed;
                assignment.Completedat = DateTime.UtcNow;
                assignment.Declinereason = cancelReason;
                await assignmentRepo.UpdateAsync(assignment, saveChanges: false);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task CancelPickupNoShowAsync(HttpContext httpContext, string orderId)
        {
            await _unitOfWork.BeginTransaction();

            try
            {
                var statusAssigned = AssignStatusEnum.ASSIGNED_PICKUP.ToString();
                var statusPickupSuccess = AssignStatusEnum.PICKUP_SUCCESS.ToString();
                var statusPickupFailed = OrderStatusEnum.PICKUPFAILED.ToString();

                var userId = _util.GetCurrentUserIdOrThrow(httpContext);

                var orderRepo = _unitOfWork.Repository<Order>();
                var assignmentRepo = _unitOfWork.Repository<Orderassignmenthistory>();
                var statusHistoryRepo = _unitOfWork.Repository<Orderstatushistory>();

                var order = await orderRepo
                    .GetAll()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Orderid == orderId);
                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                var assignedTo = await assignmentRepo
                    .GetAll()
                    .Where(a => a.Orderid == orderId && a.Status == statusAssigned)
                    .Select(a => a.Assignedto)
                    .FirstOrDefaultAsync();
                if (assignedTo == default)
                    throw new InvalidOperationException("Đơn này chưa được phân công nhận hàng.");

                if (assignedTo != userId)
                    throw new UnauthorizedAccessException("Bạn không có quyền huỷ nhận đơn này.");

                var assignment = await assignmentRepo
                    .GetAll()
                    .FirstOrDefaultAsync(a =>
                        a.Orderid == orderId &&
                        a.Status == statusAssigned &&
                        a.Assignedto == userId);

                assignment.Status = statusPickupSuccess;
                assignment.Completedat = DateTime.UtcNow;
                await assignmentRepo.UpdateAsync(assignment, saveChanges: false);

                order.Currentstatus = statusPickupFailed;
                await orderRepo.UpdateAsync(order, saveChanges: false);

                var history = new Orderstatushistory
                {
                    Statushistoryid = Guid.NewGuid(),
                    Orderid = orderId,
                    Status = statusPickupFailed,
                    Statusdescription = "Khách không có mặt, huỷ nhận hàng",
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };
                await statusHistoryRepo.InsertAsync(history, saveChanges: false);

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
                var statusAssignedDelivery = AssignStatusEnum.ASSIGNED_DELIVERY.ToString();
                var statusDelivering = OrderStatusEnum.DELIVERING.ToString();
                var orderRepo = _unitOfWork.Repository<Order>();
                var assignmentRepo = _unitOfWork.Repository<Orderassignmenthistory>();
                var statusHistoryRepo = _unitOfWork.Repository<Orderstatushistory>();
                var userId = _util.GetCurrentUserIdOrThrow(httpContext);

                var order = await orderRepo
                    .GetAll()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Orderid == orderId);
                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                var assignedTo = await assignmentRepo
                    .GetAll()
                    .Where(a => a.Orderid == orderId && a.Status == statusAssignedDelivery)
                    .Select(a => a.Assignedto)
                    .FirstOrDefaultAsync();
                if (assignedTo == default)
                    throw new InvalidOperationException("Đơn hàng này chưa được phân công giao hàng.");
                if (assignedTo != userId)
                    throw new UnauthorizedAccessException("Bạn không có quyền bắt đầu giao hàng cho đơn này.");

                var assignedOrderIds = await assignmentRepo
                    .GetAll()
                    .Where(a => a.Assignedto == userId && a.Status == statusAssignedDelivery)
                    .Select(a => a.Orderid)
                    .ToListAsync();

                var hasUnfinished = await orderRepo
                    .GetAll()
                    .AnyAsync(o =>
                        assignedOrderIds.Contains(o.Orderid) &&
                        o.Currentstatus == statusDelivering);
                if (hasUnfinished)
                    throw new InvalidOperationException("Bạn chưa hoàn thành đơn giao hàng trước đó.");

                order.Currentstatus = statusDelivering;
                await orderRepo.UpdateAsync(order, saveChanges: false);

                var history = new Orderstatushistory
                {
                    Orderid = orderId,
                    Status = statusDelivering,
                    Statusdescription = "Tài xế đang tiến hành đi giao hàng",
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };
                await statusHistoryRepo.InsertAsync(history, saveChanges: false);

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
                var statusAssignedDelivery = AssignStatusEnum.ASSIGNED_DELIVERY.ToString();
                var statusDelivering = OrderStatusEnum.DELIVERING.ToString();
                var statusDelivered = OrderStatusEnum.DELIVERED.ToString();

                var userId = _util.GetCurrentUserIdOrThrow(httpContext);

                var orderRepo = _unitOfWork.Repository<Order>();
                var assignmentRepo = _unitOfWork.Repository<Orderassignmenthistory>();
                var statusHistoryRepo = _unitOfWork.Repository<Orderstatushistory>();
                var photoRepo = _unitOfWork.Repository<Orderphoto>();

                //Lấy đơn hàng
                var order = await orderRepo
                    .GetAll()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Orderid == orderId);

                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                if (order.Currentstatus != statusDelivering)
                    throw new InvalidOperationException("Đơn hàng đang không ở bước này, vui lòng kiểm tra lại.");

                var files = httpContext.Request.Form.Files;
                if (files == null || files.Count == 0)
                    throw new ArgumentException("Vui lòng gửi ít nhất một ảnh xác nhận đã giao hàng.");

                //Cập nhật trạng thái đơn hàng
                order.Currentstatus = statusDelivered;
                await orderRepo.UpdateAsync(order, saveChanges: false);

                //Ghi nhận lịch sử trạng thái
                var history = new Orderstatushistory
                {
                    Statushistoryid = Guid.NewGuid(),
                    Orderid = orderId,
                    Status = statusDelivered,
                    Statusdescription = "Tài xế đã giao hàng thành công",
                    Notes = notes,
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };
                await statusHistoryRepo.InsertAsync(history, saveChanges: false);

                //Upload ảnh song song và lưu vào Orderphoto
                var uploadTasks = files.Select(async file =>
                {
                    var photoUrl = await _fileStorageService.UploadFileAsync(file, "order-photos");
                    var photo = new Orderphoto
                    {
                        Photoid = Guid.NewGuid(),
                        Statushistoryid = history.Statushistoryid,
                        Photourl = photoUrl
                    };
                    await photoRepo.InsertAsync(photo, saveChanges: false);
                });
                await Task.WhenAll(uploadTasks);

                /************   GHI NHẬN THANH TOÁN TIỀN MẶT  ************/
                var paymentRepo = _unitOfWork.Repository<Payment>();

                // 2) Đã có payment cho đơn này chưa?
                bool paymentExisted = paymentRepo.GetAll().Any(p => p.Orderid == orderId);

                if (!paymentExisted)
                {
                    // 3) Lấy PaymentMethodId của “Cash”
                    Guid cashMethodId = _unitOfWork.Repository<Paymentmethod>()
                                                   .GetAll()
                                                   .Where(pm => pm.Name.ToLower() == "cash")
                                                   .Select(pm => pm.Paymentmethodid)
                                                   .FirstOrDefault();

                    if (cashMethodId == Guid.Empty)
                        throw new ApplicationException("Không tìm thấy phương thức thanh toán 'Cash' trong bảng Paymentmethods.");

                    // 4) Tạo bản ghi Payment
                    var payment = new Payment
                    {
                        Paymentid = Guid.NewGuid(),
                        Orderid = orderId,
                        Paymentdate = DateTime.UtcNow,
                        Amount = order.Totalprice ?? 0m,
                        Paymentmethodid = cashMethodId,
                        Paymentstatus = "PAID",
                        Createdat = DateTime.UtcNow,
                        Collectedby = userId,
                        Isreturnedtoadmin = false
                    };

                    await paymentRepo.InsertAsync(payment, saveChanges: false);
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }

            /* ===== Schedule job & lưu JobId (ngoài transaction) ===== */
            var jobId = _jobService.ScheduleAutoComplete(orderId, DateTime.UtcNow);

            await _unitOfWork.Repository<Order>()     // dùng Repo đã có extension
                             .ExecuteUpdateAsync(
                                 set => set.SetProperty(o => o.AutoCompleteJobId, jobId),
                                 o => o.Orderid == orderId);
        }

        public async Task ConfirmOrderDeliverySuccessAsync(HttpContext httpContext, string orderId)
        {
            await _unitOfWork.BeginTransaction();

            try
            {
                var statusDelivered = OrderStatusEnum.DELIVERED.ToString();
                var statusDeliverySuccess = AssignStatusEnum.DELIVERY_SUCCESS.ToString();
                var statusAssignedDelivery = AssignStatusEnum.ASSIGNED_DELIVERY.ToString();
                var userId = _util.GetCurrentUserIdOrThrow(httpContext);
                var orderRepo = _unitOfWork.Repository<Order>();
                var assignmentRepo = _unitOfWork.Repository<Orderassignmenthistory>();
                var statusHistoryRepo = _unitOfWork.Repository<Orderstatushistory>();

                var order = await orderRepo
                    .GetAll()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Orderid == orderId);
                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                var assignedTo = await assignmentRepo
                    .GetAll()
                    .Where(a => a.Orderid == orderId && a.Status == statusAssignedDelivery)
                    .Select(a => a.Assignedto)
                    .FirstOrDefaultAsync();
                if (assignedTo == default)
                    throw new InvalidOperationException("Đơn hàng này chưa được phân công giao hàng.");

                if (assignedTo != userId)
                    throw new UnauthorizedAccessException("Bạn không có quyền hoàn thành bước giao hàng này.");

                if (order.Currentstatus != statusDelivered)
                    throw new InvalidOperationException("Đơn hàng đang không ở bước này, vui lòng kiểm tra lại.");

                var assignment = await assignmentRepo
                    .GetAll()
                    .FirstOrDefaultAsync(a =>
                        a.Orderid == orderId &&
                        a.Status == statusAssignedDelivery &&
                        a.Assignedto == userId);

                assignment.Status = statusDeliverySuccess;
                assignment.Completedat = DateTime.UtcNow;
                await assignmentRepo.UpdateAsync(assignment, saveChanges: false);

                var history = new Orderstatushistory
                {
                    Orderid = orderId,
                    Status = statusDeliverySuccess,
                    Statusdescription = "Tài xế đã giao hàng và về tới nơi",
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };
                await statusHistoryRepo.InsertAsync(history, saveChanges: false);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task CancelAssignedDeliveryAsync(HttpContext httpContext, string orderId, string cancelReason)
        {
            if (string.IsNullOrWhiteSpace(cancelReason))
                throw new ArgumentException("Lý do huỷ không được để trống.");

            await _unitOfWork.BeginTransaction();
            try
            {
                var statusAssigned = AssignStatusEnum.ASSIGNED_DELIVERY.ToString();
                var statusDeliveryFailed = AssignStatusEnum.DELIVERY_FAILED.ToString();

                var userId = _util.GetCurrentUserIdOrThrow(httpContext);
                var orderRepo = _unitOfWork.Repository<Order>();
                var assignmentRepo = _unitOfWork.Repository<Orderassignmenthistory>();

                var order = await orderRepo
                    .GetAll().AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Orderid == orderId);
                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                var assignedTo = await assignmentRepo
                    .GetAll()
                    .Where(a => a.Orderid == orderId && a.Status == statusAssigned)
                    .Select(a => a.Assignedto)
                    .FirstOrDefaultAsync();
                if (assignedTo == default)
                    throw new InvalidOperationException("Đơn này chưa được phân công giao hàng.");

                if (assignedTo != userId)
                    throw new UnauthorizedAccessException("Bạn không có quyền huỷ giao hàng này.");

                var assignment = await assignmentRepo
                    .GetAll()
                    .FirstOrDefaultAsync(a =>
                        a.Orderid == orderId &&
                        a.Status == statusAssigned &&
                        a.Assignedto == userId);

                assignment.Status = statusDeliveryFailed;
                assignment.Completedat = DateTime.UtcNow;
                assignment.Declinereason = cancelReason;
                await assignmentRepo.UpdateAsync(assignment, saveChanges: false);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task CancelDeliveryNoShowAsync(HttpContext httpContext, string orderId)
        {
            await _unitOfWork.BeginTransaction();

            try
            {
                var statusAssigned = AssignStatusEnum.ASSIGNED_DELIVERY.ToString();
                var statusDeliverySucc = AssignStatusEnum.DELIVERY_SUCCESS.ToString();
                var statusDeliveryFail = OrderStatusEnum.DELIVERYFAILED.ToString();
                var userId = _util.GetCurrentUserIdOrThrow(httpContext);
                var orderRepo = _unitOfWork.Repository<Order>();
                var assignmentRepo = _unitOfWork.Repository<Orderassignmenthistory>();
                var statusHistoryRepo = _unitOfWork.Repository<Orderstatushistory>();

                var order = await orderRepo
                    .GetAll()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Orderid == orderId);
                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                var assignedTo = await assignmentRepo
                    .GetAll()
                    .Where(a => a.Orderid == orderId && a.Status == statusAssigned)
                    .Select(a => a.Assignedto)
                    .FirstOrDefaultAsync();
                if (assignedTo == default)
                    throw new InvalidOperationException("Đơn này chưa được phân công giao hàng.");

                if (assignedTo != userId)
                    throw new UnauthorizedAccessException("Bạn không có quyền huỷ giao hàng này.");

                var assignment = await assignmentRepo
                    .GetAll()
                    .FirstOrDefaultAsync(a =>
                        a.Orderid == orderId &&
                        a.Status == statusAssigned &&
                        a.Assignedto == userId);

                assignment.Status = statusDeliverySucc;
                assignment.Completedat = DateTime.UtcNow;
                await assignmentRepo.UpdateAsync(assignment, saveChanges: false);

                order.Currentstatus = statusDeliveryFail;
                await orderRepo.UpdateAsync(order, saveChanges: false);

                var history = new Orderstatushistory
                {
                    Statushistoryid = Guid.NewGuid(),
                    Orderid = orderId,
                    Status = statusDeliveryFail,
                    Statusdescription = "Khách không có mặt, huỷ giao hàng",
                    Updatedby = userId,
                    Createdat = DateTime.UtcNow
                };
                await statusHistoryRepo.InsertAsync(history, saveChanges: false);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<DriverStatisticsResponse> GetDailyStatisticsAsync(HttpContext httpContext, DateTime date)
        {
            var driverId = _util.GetCurrentUserIdOrThrow(httpContext);
            date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var start = date;
            var end = start.AddDays(1);

            var pickupCount = await _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAll()
                .CountAsync(a =>
                    a.Assignedto == driverId &&
                    a.Status == AssignStatusEnum.PICKUP_SUCCESS.ToString() &&
                    a.Completedat >= start &&
                    a.Completedat < end);

            var deliveryEvents = await _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAll()
                .Where(a =>
                    a.Assignedto == driverId &&
                    a.Status == AssignStatusEnum.DELIVERY_SUCCESS.ToString() &&
                    a.Completedat >= start &&
                    a.Completedat < end)
                .ToListAsync();

            var deliveryCount = deliveryEvents.Count;
            var deliveredOrderIds = deliveryEvents.Select(a => a.Orderid).Distinct().ToList();

            var cashPayments = await _unitOfWork.Repository<Payment>()
                .GetAll()
                .Include(p => p.Paymentmethod)
                .Where(p =>
                    p.Collectedby == driverId &&
                    p.Paymentmethod.Name == "Cash" &&
                    deliveredOrderIds.Contains(p.Orderid))
                .ToListAsync();

            var cashOrdersCount = cashPayments
                .Select(p => p.Orderid)
                .Distinct()
                .Count();

            var cashTotalAmount = cashPayments
                .Sum(p => p.Amount);

            return new DriverStatisticsResponse
            {
                PeriodStart = start,
                PeriodEnd = end,
                TotalOrdersCount = pickupCount + deliveryCount,
                CashOrdersCount = cashOrdersCount,
                CashTotalAmount = cashTotalAmount
            };
        }


        public async Task<DriverStatisticsResponse> GetWeeklyStatisticsAsync(HttpContext httpContext, DateTime dateInWeek)
        {
            var driverId = _util.GetCurrentUserIdOrThrow(httpContext);
            dateInWeek = DateTime.SpecifyKind(dateInWeek.Date, DateTimeKind.Utc);
            var diff = (7 + (dateInWeek.DayOfWeek - DayOfWeek.Monday)) % 7;
            var start = dateInWeek.AddDays(-diff);
            var end = start.AddDays(7);

            var pickupCount = await _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAll()
                .CountAsync(a =>
                    a.Assignedto == driverId &&
                    a.Status == AssignStatusEnum.PICKUP_SUCCESS.ToString() &&
                    a.Completedat >= start &&
                    a.Completedat < end);

            var deliveryEvents = await _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAll()
                .Where(a =>
                    a.Assignedto == driverId &&
                    a.Status == AssignStatusEnum.DELIVERY_SUCCESS.ToString() &&
                    a.Completedat >= start &&
                    a.Completedat < end)
                .ToListAsync();

            var deliveryCount = deliveryEvents.Count;
            var deliveredOrderIds = deliveryEvents.Select(a => a.Orderid).Distinct().ToList();

            var cashPayments = await _unitOfWork.Repository<Payment>()
                .GetAll()
                .Include(p => p.Paymentmethod)
                .Where(p =>
                    p.Collectedby == driverId &&
                    p.Paymentmethod.Name == "Cash" &&
                    deliveredOrderIds.Contains(p.Orderid))
                .ToListAsync();

            var cashOrdersCount = cashPayments.Select(p => p.Orderid).Distinct().Count();
            var cashTotalAmount = cashPayments.Sum(p => p.Amount);

            return new DriverStatisticsResponse
            {
                PeriodStart = start,
                PeriodEnd = end,
                TotalOrdersCount = pickupCount + deliveryCount,
                CashOrdersCount = cashOrdersCount,
                CashTotalAmount = cashTotalAmount
            };
        }


        public async Task<DriverStatisticsResponse> GetMonthlyStatisticsAsync(HttpContext httpContext, int year, int month)
        {
            if (month < 1 || month > 12)
                throw new ArgumentException("Tháng phải nằm trong khoảng 1–12.");

            var driverId = _util.GetCurrentUserIdOrThrow(httpContext);
            var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);

            var pickupCount = await _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAll()
                .CountAsync(a =>
                    a.Assignedto == driverId &&
                    a.Status == AssignStatusEnum.PICKUP_SUCCESS.ToString() &&
                    a.Completedat >= start &&
                    a.Completedat < end);

            var deliveryEvents = await _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAll()
                .Where(a =>
                    a.Assignedto == driverId &&
                    a.Status == AssignStatusEnum.DELIVERY_SUCCESS.ToString() &&
                    a.Completedat >= start &&
                    a.Completedat < end)
                .ToListAsync();

            var deliveryCount = deliveryEvents.Count;
            var deliveredOrderIds = deliveryEvents.Select(a => a.Orderid).Distinct().ToList();

            var cashPayments = await _unitOfWork.Repository<Payment>()
                .GetAll()
                .Include(p => p.Paymentmethod)
                .Where(p =>
                    p.Collectedby == driverId &&
                    p.Paymentmethod.Name == "Cash" &&
                    deliveredOrderIds.Contains(p.Orderid))
                .ToListAsync();

            var cashOrdersCount = cashPayments.Select(p => p.Orderid).Distinct().Count();
            var cashTotalAmount = cashPayments.Sum(p => p.Amount);

            return new DriverStatisticsResponse
            {
                PeriodStart = start,
                PeriodEnd = end,
                TotalOrdersCount = pickupCount + deliveryCount,
                CashOrdersCount = cashOrdersCount,
                CashTotalAmount = cashTotalAmount
            };
        }

        private IQueryable<Orderassignmenthistory> BaseAssignmentQuery(
            Guid driverId, DateTime start, DateTime end)
        {
            string pickup = AssignStatusEnum.PICKUP_SUCCESS.ToString();
            string delivery = AssignStatusEnum.DELIVERY_SUCCESS.ToString();

            return _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAll()
                .Include(a => a.Order)
                    .ThenInclude(o => o.Payments)
                        .ThenInclude(p => p.Paymentmethod)
                .Where(a => a.Assignedto == driverId
                         && a.Completedat >= start
                         && a.Completedat < end
                         && (a.Status == pickup || a.Status == delivery))
                .OrderByDescending(a => a.Completedat);
        }

        public async Task<List<DriverStatisticsListResponse>> GetDailyStatisticsListAsync(HttpContext httpContext, DateTime date)
        {
            var driverId = _util.GetCurrentUserIdOrThrow(httpContext);
            date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

            var events = await BaseAssignmentQuery(driverId, date, date.AddDays(1))

                .ToListAsync();

            return events.Select(a => new DriverStatisticsListResponse
            {
                OrderId = a.Orderid,
                CompletedAt = DateTime.SpecifyKind(a.Completedat!.Value, DateTimeKind.Utc),
                TotalPrice = a.Order.Totalprice ?? 0m,
                AssignmentStatus = a.Status!,
                PaymentName = a.Status == AssignStatusEnum.PICKUP_SUCCESS.ToString()
                                     ? a.Order.Payments
                                           .OrderByDescending(p => p.Paymentdate)
                                           .FirstOrDefault()?
                                           .Paymentmethod?.Name
                                     : null
            }).ToList();
        }

        public async Task<List<DriverStatisticsListResponse>> GetWeeklyStatisticsListAsync(HttpContext httpContext, DateTime dateInWeek)
        {
            var driverId = _util.GetCurrentUserIdOrThrow(httpContext);
            dateInWeek = DateTime.SpecifyKind(dateInWeek.Date, DateTimeKind.Utc);
            int diff = (7 + (dateInWeek.DayOfWeek - DayOfWeek.Monday)) % 7;
            var start = dateInWeek.AddDays(-diff);
            var end = start.AddDays(7);

            var events = await BaseAssignmentQuery(driverId, start, end)

                .ToListAsync();

            return events.Select(a => new DriverStatisticsListResponse
            {
                OrderId = a.Orderid,
                CompletedAt = DateTime.SpecifyKind(a.Completedat!.Value, DateTimeKind.Utc),
                TotalPrice = a.Order.Totalprice ?? 0m,
                AssignmentStatus = a.Status!,
                PaymentName = a.Status == AssignStatusEnum.PICKUP_SUCCESS.ToString()
                                     ? a.Order.Payments
                                           .OrderByDescending(p => p.Paymentdate)
                                           .FirstOrDefault()?
                                           .Paymentmethod?.Name
                                     : null
            }).ToList();
        }

        public async Task<List<DriverStatisticsListResponse>> GetMonthlyStatisticsListAsync(HttpContext httpContext, int year, int month)
        {
            if (month is < 1 or > 12)
                throw new ArgumentException("Tháng phải nằm trong khoảng 1–12.");

            var driverId = _util.GetCurrentUserIdOrThrow(httpContext);
            var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);

            var events = await BaseAssignmentQuery(driverId, start, end)

                .ToListAsync();

            return events.Select(a => new DriverStatisticsListResponse
            {
                OrderId = a.Orderid,
                CompletedAt = DateTime.SpecifyKind(a.Completedat!.Value, DateTimeKind.Utc),
                TotalPrice = a.Order.Totalprice ?? 0m,
                AssignmentStatus = a.Status!,
                PaymentName = a.Status == AssignStatusEnum.PICKUP_SUCCESS.ToString()
                                     ? a.Order.Payments
                                           .OrderByDescending(p => p.Paymentdate)
                                           .FirstOrDefault()?
                                           .Paymentmethod?.Name
                                     : null
            }).ToList();
        }
    }
}
