using LaundryService.Domain.Entities;
using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
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
        //xong
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
        //xongg
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
        //xong
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
        //xong
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
        //xong
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
        //xong
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
        //xong
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
                    throw new UnauthorizedAccessException("Bạn không có quyền xác nhận giao hàng cho đơn này.");

                if (order.Currentstatus != statusDelivering)
                    throw new InvalidOperationException("Đơn hàng đang không ở bước này, vui lòng kiểm tra lại.");

                var files = httpContext.Request.Form.Files;
                if (files == null || files.Count == 0)
                    throw new ArgumentException("Vui lòng gửi ít nhất một ảnh xác nhận đã giao hàng.");

                order.Currentstatus = statusDelivered;
                await orderRepo.UpdateAsync(order, saveChanges: false);

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

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }
        //xong
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
        //xong
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
        //xong
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

    }
}
