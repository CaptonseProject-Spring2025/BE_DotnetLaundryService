using LaundryService.Domain.Entities;
using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Http;
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

        public DriverService(IUnitOfWork unitOfWork, IUtil util, IFileStorageService fileStorageService)
        {
            _unitOfWork = unitOfWork;
            _util = util;
            _fileStorageService = fileStorageService;
        }

        public async Task StartOrderPickupAsync(HttpContext httpContext, string orderId)
        {
            await _unitOfWork.BeginTransaction();
            try
            {
                var statusAssignedPickup = AssignStatusEnum.ASSIGNED_PICKUP.ToString();
                var statusScheduledPickup = OrderStatusEnum.SCHEDULED_PICKUP.ToString();
                var statusPickingUp = OrderStatusEnum.PICKINGUP.ToString();

                var assignmentRepo = _unitOfWork.Repository<Orderassignmenthistory>();
                var orderRepo = _unitOfWork.Repository<Order>();
                var statusHistoryRepo = _unitOfWork.Repository<Orderstatushistory>();

                var userId = _util.GetCurrentUserIdOrThrow(httpContext);

                // Lấy thông tin đơn hàng trước
                var order = orderRepo
                    .GetAll()
                    .FirstOrDefault(o => o.Orderid == orderId);

                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                if (order.Currentstatus != statusScheduledPickup)
                    throw new InvalidOperationException("Đơn hàng đang không ở bước này, vui lòng kiểm tra lại.");

                // Lấy assignment của đơn và phải đúng tài xế đang thao tác
                var assignment = assignmentRepo
                    .GetAll()
                    .FirstOrDefault(a => a.Orderid == orderId
                                      && a.Status == statusAssignedPickup
                                      && a.Assignedto == userId);

                if (assignment == null)
                    throw new UnauthorizedAccessException("Bạn không được giao thực hiện đơn nhận hàng này hoặc trạng thái công việc không hợp lệ.");

                // Lấy danh sách các đơn tài xế được gán ở trạng thái ASSIGNED_PICKUP
                var assignedOrderIds = assignmentRepo
                    .GetAll()
                    .Where(a => a.Assignedto == userId && a.Status == statusAssignedPickup)
                    .Select(a => a.Orderid)
                    .ToList();

                // Kiểm tra nếu có đơn nào đang ở trạng thái PICKINGUP
                var uncompletedOrder = orderRepo
                    .GetAll()
                    .Any(o => assignedOrderIds.Contains(o.Orderid)
                              && o.Currentstatus == statusPickingUp);

                if (uncompletedOrder)
                    throw new InvalidOperationException("Bạn chưa hoàn thành đơn nhận hàng trước đó.");

                // Cập nhật trạng thái đơn hàng
                order.Currentstatus = statusPickingUp;
                await orderRepo.UpdateAsync(order, saveChanges: false);

                // Ghi nhận lịch sử trạng thái
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

                //Lấy đơn hàng
                var order = orderRepo
                    .GetAll()
                    .FirstOrDefault(o => o.Orderid == orderId);

                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                //Trạng thái đơn phải là PICKEDUP
                if (order.Currentstatus != statusPickedUp)
                    throw new InvalidOperationException("Đơn hàng đang không ở bước này, vui lòng kiểm tra lại.");

                //Lấy assignment: đúng đơn, đúng tài xế, đúng trạng thái
                var assignment = assignmentRepo
                    .GetAll()
                    .FirstOrDefault(a =>
                        a.Orderid == orderId &&
                        a.Status == statusAssignedPickup &&
                        a.Assignedto == userId);

                if (assignment == null)
                    throw new UnauthorizedAccessException("Bạn không được giao thực hiện đơn này hoặc trạng thái công việc không hợp lệ.");

                //Cập nhật trạng thái assignment
                assignment.Status = statusPickupSuccess;
                assignment.Completedat = DateTime.UtcNow;
                await assignmentRepo.UpdateAsync(assignment, saveChanges: false);

                //Ghi nhận lịch sử trạng thái
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

                //Lấy đơn hàng
                var order = orderRepo.GetAll().FirstOrDefault(o => o.Orderid == orderId);
                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                //Kiểm tra trạng thái đơn hàng
                if (order.Currentstatus != statusPickingUp)
                    throw new InvalidOperationException("Đơn hàng đang không ở bước này, vui lòng kiểm tra lại.");

                //Kiểm tra assignment hợp lệ
                var assignment = assignmentRepo
                    .GetAll()
                    .FirstOrDefault(a =>
                        a.Orderid == orderId &&
                        a.Status == statusAssignedPickup &&
                        a.Assignedto == userId);

                if (assignment == null)
                    throw new UnauthorizedAccessException("Bạn không được giao thực hiện đơn nhận hàng này hoặc trạng thái công việc không hợp lệ.");

                //Check ảnh hợp lệ
                var files = httpContext.Request.Form.Files;
                if (files?.Count == 0)
                    throw new ArgumentException("Vui lòng chụp và gửi ít nhất một ảnh khi nhận hàng.");

                //Cập nhật trạng thái đơn hàng
                order.Currentstatus = statusPickedUp;
                await orderRepo.UpdateAsync(order, saveChanges: false);

                //Ghi nhận lịch sử trạng thái
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

                //Upload ảnh song song và lưu lại
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

        public async Task CancelAssignedPickupAsync(HttpContext httpContext, string orderId, string cancelReason)
        {
            if (string.IsNullOrWhiteSpace(cancelReason))
                throw new ArgumentException("Lý do huỷ không được để trống.");

            await _unitOfWork.BeginTransaction();
            try
            {
                var statusAssigned = AssignStatusEnum.ASSIGNED_PICKUP.ToString();
                var statusPickedUp = OrderStatusEnum.PICKEDUP.ToString();
                var statusPickupFailed = OrderStatusEnum.PICKUPFAILED.ToString();

                var userId = _util.GetCurrentUserIdOrThrow(httpContext);

                var orderRepo = _unitOfWork.Repository<Order>();
                var assignmentRepo = _unitOfWork.Repository<Orderassignmenthistory>();
                var statusHistoryRepo = _unitOfWork.Repository<Orderstatushistory>();
                var photoRepo = _unitOfWork.Repository<Orderphoto>();

                //Lấy và kiểm tra đơn hàng
                var order = orderRepo
                    .GetAll()
                    .FirstOrDefault(o => o.Orderid == orderId);

                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                //Chỉ cho phép huỷ nếu assignment đang ở trạng thái ASSIGNED_PICKUP
                var assignment = assignmentRepo
                    .GetAll()
                    .FirstOrDefault(a =>
                        a.Orderid == orderId &&
                        a.Assignedto == userId &&
                        a.Status == statusAssigned);

                if (assignment == null)
                    throw new UnauthorizedAccessException("Bạn không được giao thực hiện đơn này hoặc không thể huỷ ở bước hiện tại.");

                //Kiểm tra nếu đã PICKEDUP thì không được huỷ
                var hasPickedUp = statusHistoryRepo
                    .GetAll()
                    .Any(h =>
                        h.Orderid == orderId &&
                        h.Status == statusPickedUp &&
                        h.Updatedby == userId);

                if (hasPickedUp)
                    throw new InvalidOperationException("Bạn đã nhận hàng, không thể huỷ đơn này.");

                //Kiểm tra ảnh đính kèm
                var files = httpContext.Request.Form.Files;
                if (files?.Count == 0)
                    throw new ArgumentException("Vui lòng gửi ít nhất một ảnh chứng minh lý do huỷ đơn.");

                //Cập nhật trạng thái assignment
                assignment.Status = AssignStatusEnum.PICKUP_FAILED.ToString();
                assignment.Completedat = DateTime.UtcNow;
                await assignmentRepo.UpdateAsync(assignment, saveChanges: false);

                //Cập nhật trạng thái đơn hàng
                order.Currentstatus = statusPickupFailed;
                await orderRepo.UpdateAsync(order, saveChanges: false);

                //Ghi nhận lịch sử trạng thái
                var history = new Orderstatushistory
                {
                    Statushistoryid = Guid.NewGuid(),
                    Orderid = orderId,
                    Status = statusPickupFailed,
                    Statusdescription = "Tài xế đã huỷ nhận đơn hàng",
                    Notes = cancelReason,
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
                var statusScheduledDelivery = OrderStatusEnum.SCHEDULED_DELIVERY.ToString();
                var statusDelivering = OrderStatusEnum.DELIVERING.ToString();

                var assignmentRepo = _unitOfWork.Repository<Orderassignmenthistory>();
                var orderRepo = _unitOfWork.Repository<Order>();
                var statusHistoryRepo = _unitOfWork.Repository<Orderstatushistory>();

                var userId = _util.GetCurrentUserIdOrThrow(httpContext);

                //Lấy thông tin đơn hàng trước
                var order = orderRepo
                    .GetAll()
                    .FirstOrDefault(o => o.Orderid == orderId);

                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                if (order.Currentstatus != statusScheduledDelivery)
                    throw new InvalidOperationException("Đơn hàng đang không ở bước này, vui lòng kiểm tra lại.");

                //Lấy assignment của đơn và phải đúng tài xế đang thao tác
                var assignment = assignmentRepo
                    .GetAll()
                    .FirstOrDefault(a => a.Orderid == orderId
                                      && a.Status == statusAssignedDelivery
                                      && a.Assignedto == userId);

                if (assignment == null)
                    throw new UnauthorizedAccessException("Bạn không được giao thực hiện đơn giao hàng này hoặc trạng thái công việc không hợp lệ.");

                //Lấy danh sách các đơn tài xế được gán ở trạng thái ASSIGNED_DELIVERY
                var assignedOrderIds = assignmentRepo
                    .GetAll()
                    .Where(a => a.Assignedto == userId && a.Status == statusAssignedDelivery)
                    .Select(a => a.Orderid)
                    .ToList();

                //Kiểm tra nếu có đơn nào đang ở trạng thái DELIVERING
                var uncompletedOrder = orderRepo
                    .GetAll()
                    .Any(o => assignedOrderIds.Contains(o.Orderid)
                              && o.Currentstatus == statusDelivering);

                if (uncompletedOrder)
                    throw new InvalidOperationException("Bạn chưa hoàn thành đơn giao hàng trước đó.");

                //Cập nhật trạng thái đơn hàng
                order.Currentstatus = statusDelivering;
                await orderRepo.UpdateAsync(order, saveChanges: false);

                //Ghi nhận lịch sử trạng thái
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
                var order = orderRepo
                    .GetAll()
                    .FirstOrDefault(o => o.Orderid == orderId);

                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                //Kiểm tra trạng thái phải là DELIVERING
                if (order.Currentstatus != statusDelivering)
                    throw new InvalidOperationException("Đơn hàng đang không ở bước này, vui lòng kiểm tra lại.");

                //Kiểm tra assignment: đúng đơn, đúng tài xế, đúng trạng thái
                var assignment = assignmentRepo
                    .GetAll()
                    .FirstOrDefault(a =>
                        a.Orderid == orderId &&
                        a.Status == statusAssignedDelivery &&
                        a.Assignedto == userId);

                if (assignment == null)
                    throw new UnauthorizedAccessException("Bạn không được giao thực hiện đơn giao hàng này hoặc trạng thái công việc không hợp lệ.");

                //Kiểm tra ảnh đính kèm
                var files = httpContext.Request.Form.Files;
                if (files?.Count == 0)
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

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
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

                //Lấy đơn hàng
                var order = orderRepo
                    .GetAll()
                    .FirstOrDefault(o => o.Orderid == orderId);

                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                //Trạng thái đơn phải là PICKEDUP
                if (order.Currentstatus != statusDelivered)
                    throw new InvalidOperationException("Đơn hàng đang không ở bước này, vui lòng kiểm tra lại.");

                //Lấy assignment: đúng đơn, đúng tài xế, đúng trạng thái
                var assignment = assignmentRepo
                    .GetAll()
                    .FirstOrDefault(a =>
                        a.Orderid == orderId &&
                        a.Status == statusAssignedDelivery &&
                        a.Assignedto == userId);

                if (assignment == null)
                    throw new UnauthorizedAccessException("Bạn không được giao thực hiện đơn này hoặc trạng thái công việc không hợp lệ.");

                //Cập nhật trạng thái assignment
                assignment.Status = statusDeliverySuccess;
                assignment.Completedat = DateTime.UtcNow;
                await assignmentRepo.UpdateAsync(assignment, saveChanges: false);

                //Ghi nhận lịch sử trạng thái
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
                var statusDelivered = OrderStatusEnum.DELIVERED.ToString();
                var statusDeliveryFailed = OrderStatusEnum.DELIVERYFAILED.ToString();

                var userId = _util.GetCurrentUserIdOrThrow(httpContext);

                var orderRepo = _unitOfWork.Repository<Order>();
                var assignmentRepo = _unitOfWork.Repository<Orderassignmenthistory>();
                var statusHistoryRepo = _unitOfWork.Repository<Orderstatushistory>();
                var photoRepo = _unitOfWork.Repository<Orderphoto>();

                //Lấy và kiểm tra đơn hàng
                var order = orderRepo
                    .GetAll()
                    .FirstOrDefault(o => o.Orderid == orderId);

                if (order == null)
                    throw new KeyNotFoundException("Đơn hàng không tồn tại.");

                //Chỉ cho phép huỷ nếu assignment đang ở trạng thái ASSIGNED_DELIVERY
                var assignment = assignmentRepo
                    .GetAll()
                    .FirstOrDefault(a =>
                        a.Orderid == orderId &&
                        a.Assignedto == userId &&
                        a.Status == statusAssigned);

                if (assignment == null)
                    throw new UnauthorizedAccessException("Bạn không được giao thực hiện đơn này hoặc không thể huỷ ở bước hiện tại.");

                //Kiểm tra nếu đã DELIVERED thì không được huỷ
                var hasDelivered = statusHistoryRepo
                    .GetAll()
                    .Any(h =>
                        h.Orderid == orderId &&
                        h.Status == statusDelivered &&
                        h.Updatedby == userId);

                if (hasDelivered)
                    throw new InvalidOperationException("Bạn đã giao hàng, không thể huỷ đơn này.");

                //Kiểm tra ảnh đính kèm
                var files = httpContext.Request.Form.Files;
                if (files?.Count == 0)
                    throw new ArgumentException("Vui lòng gửi ít nhất một ảnh chứng minh lý do huỷ đơn.");

                //Cập nhật trạng thái assignment
                assignment.Status = AssignStatusEnum.DELIVERY_FAILED.ToString();
                assignment.Completedat = DateTime.UtcNow;
                await assignmentRepo.UpdateAsync(assignment, saveChanges: false);

                //Cập nhật trạng thái đơn hàng
                order.Currentstatus = statusDeliveryFailed;
                await orderRepo.UpdateAsync(order, saveChanges: false);

                //Ghi nhận lịch sử trạng thái
                var history = new Orderstatushistory
                {
                    Statushistoryid = Guid.NewGuid(),
                    Orderid = orderId,
                    Status = statusDeliveryFailed,
                    Statusdescription = "Tài xế đã huỷ giao đơn hàng",
                    Notes = cancelReason,
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
