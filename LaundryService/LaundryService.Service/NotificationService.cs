using LaundryService.Domain.Interfaces.Services;
using LaundryService.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LaundryService.Domain.Entities;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using LaundryService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LaundryService.Service
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUtil _util;

        public NotificationService(IUnitOfWork unitOfWork, IUtil util)
        {
            _unitOfWork = unitOfWork;
            _util = util;
        }

        public async Task<IEnumerable<NotificationResponse>> GetNotificationsByUserIdAsync(HttpContext httpContext)
        {
            var userId = _util.GetCurrentUserIdOrThrow(httpContext);

            var notifications = await _unitOfWork.Repository<Notification>()
                .GetAllAsync(n => n.Userid == userId);

            var sortedNotifications = notifications
        .OrderByDescending(n => n.Createdat ?? DateTime.MinValue)
        .Select(n => new NotificationResponse
        {
            NotificationId = n.Notificationid,
            UserId = n.Userid,
            Title = n.Title,
            Message = n.Message,
            NotificationType = n.Notificationtype,
            IsRead = n.Isread ?? false,
            CustomerId = n.Customerid,
            OrderId = n.Orderid,
            CreatedAt = n.Createdat ?? DateTime.UtcNow,
            IsPushEnabled = n.Ispushenabled ?? false
        })
        .ToList();

            return sortedNotifications;
        }


        public async Task DeleteNotificationAsync(Guid notificationId)
        {
            var notification = await _unitOfWork.Repository<Notification>().FindAsync(notificationId);
            if (notification == null)
            {
                throw new KeyNotFoundException("Notification not found.");
            }

            await _unitOfWork.Repository<Notification>().DeleteAsync(notification);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task MarkAsReadAsync(Guid notificationId)
        {
            var notification = await _unitOfWork.Repository<Notification>().FindAsync(notificationId);
            if (notification == null)
            {
                throw new KeyNotFoundException("Notification not found.");
            }

            notification.Isread = true;
            await _unitOfWork.Repository<Notification>().UpdateAsync(notification);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task MarkAllUserNotificationsAsReadAsync(HttpContext httpContext)
        {
            var userId = _util.GetCurrentUserIdOrThrow(httpContext);

            var notifications = await _unitOfWork.Repository<Notification>()
                .GetAll()
                .Where(n => n.Userid == userId && (n.Isread != true))
                .ToListAsync();

            if (notifications == null || notifications.Count == 0)
            {
                throw new KeyNotFoundException("Không tìm thấy thông báo chưa đọc của người dùng hiện tại.");
            }

            notifications.ForEach(n => n.Isread = true);

            await _unitOfWork.Repository<Notification>().UpdateRangeAsync(notifications, saveChanges: false);

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteAllNotificationsOfCurrentUserAsync(HttpContext context)
        {
            var userId = _util.GetCurrentUserIdOrThrow(context);

            var notifications = await _unitOfWork.Repository<Notification>()
                .GetAllAsync(n => n.Userid == userId);

            if (notifications.Any())
            {
                foreach (var n in notifications)
                    await _unitOfWork.Repository<Notification>().DeleteAsync(n);

                await _unitOfWork.SaveChangesAsync();
            }
        }

        public async Task CreateOrderPlacedNotificationAsync(Guid userId, string orderId)
        {
            var notification = new Notification
            {
                Notificationid = Guid.NewGuid(),
                Userid = userId,
                Title = "Thông báo đặt hàng",
                Message = "Bạn đã đặt hàng thành công. Vui lòng chờ nhân viên liên hệ.",
                Notificationtype = "OrderPlaced",
                Orderid = orderId,
                Createdat = DateTime.UtcNow,
                Ispushenabled = true,
                Isread = false
            };

            await _unitOfWork.Repository<Notification>().InsertAsync(notification);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task CreateOrderConfirmedNotificationAsync(Guid userId, string orderId)
        {
            var notification = new Notification
            {
                Notificationid = Guid.NewGuid(),
                Userid = userId,
                Title = "Xác nhận đơn hàng",
                Message = "Đơn hàng của bạn đã được xác nhận thành công.",
                Notificationtype = "OrderConfirmed",
                Orderid = orderId,
                Createdat = DateTime.UtcNow,
                Ispushenabled = true,
                Isread = false
            };

            await _unitOfWork.Repository<Notification>().InsertAsync(notification);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task CreateOrderCanceledNotificationAsync(Guid userId, string orderId)
        {
            var notification = new Notification
            {
                Notificationid = Guid.NewGuid(),
                Userid = userId,
                Title = "Hủy đơn hàng",
                Message = "Đơn hàng của bạn đã được hủy thành công.",
                Notificationtype = "OrderCancelled",
                Orderid = orderId,
                Createdat = DateTime.UtcNow,
                Ispushenabled = true,
                Isread = false
            };

            await _unitOfWork.Repository<Notification>().InsertAsync(notification);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task CreatePickupScheduledNotificationAsync(Guid customerId, string orderId)
        {
            var notification = new Notification
            {
                Notificationid = Guid.NewGuid(),
                Userid = customerId,
                Title = "Thông báo nhận hàng",
                Message = "Đơn hàng của bạn đã được lên lịch để tài xế đến nhận. Vui lòng chuẩn bị hàng sẵn sàng!",
                Notificationtype = NotificationType.PickupScheduled.ToString(),
                Orderid = orderId,
                Createdat = DateTime.UtcNow,
                Ispushenabled = true,
                Isread = false
            };

            await _unitOfWork.Repository<Notification>().InsertAsync(notification);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task CreatePickupStartedNotificationAsync(Guid userId, string orderId)
        {
            var notification = new Notification
            {
                Notificationid = Guid.NewGuid(),
                Userid = userId,
                Title = "Thông báo nhận hàng",
                Message = "Tài xế đã bắt đầu đi đến nhận đơn hàng của bạn.",
                Notificationtype = NotificationType.PickupStarted.ToString(),
                Orderid = orderId,
                Createdat = DateTime.UtcNow,
                Ispushenabled = true,
                Isread = false
            };

            await _unitOfWork.Repository<Notification>().InsertAsync(notification);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task CreateOrderPickedUpNotificationAsync(Guid userId, string orderId)
        {
            var notification = new Notification
            {
                Notificationid = Guid.NewGuid(),
                Userid = userId,
                Title = "Thông báo nhận hàng",
                Message = "Tài xế đã nhận đơn hàng thành công.",
                Notificationtype = NotificationType.PickedUp.ToString(),
                Orderid = orderId,
                Createdat = DateTime.UtcNow,
                Ispushenabled = true,
                Isread = false
            };

            await _unitOfWork.Repository<Notification>().InsertAsync(notification);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task CreateDeliveryStartedNotificationAsync(Guid userId, string orderId)
        {
            var notification = new Notification
            {
                Notificationid = Guid.NewGuid(),
                Userid = userId,
                Title = "Thông báo giao hàng",
                Message = "Tài xế đã bắt đầu đi giao hàng đến địa chỉ của bạn.",
                Notificationtype = NotificationType.DeliveryStarted.ToString(),
                Orderid = orderId,
                Createdat = DateTime.UtcNow,
                Ispushenabled = true,
                Isread = false
            };

            await _unitOfWork.Repository<Notification>().InsertAsync(notification);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task CreateOrderDeliveredNotificationAsync(Guid userId, string orderId)
        {
            var notification = new Notification
            {
                Notificationid = Guid.NewGuid(),
                Userid = userId,
                Title = "Thông báo giao hàng",
                Message = "Tài xế đã giao đơn hàng đến bạn thành công.",
                Notificationtype = NotificationType.Delivered.ToString(),
                Orderid = orderId,
                Createdat = DateTime.UtcNow,
                Ispushenabled = true,
                Isread = false
            };

            await _unitOfWork.Repository<Notification>().InsertAsync(notification);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task CreateThankYouNotificationAsync(Guid userId, string orderId)
        {
            var notification = new Notification
            {
                Notificationid = Guid.NewGuid(),
                Userid = userId,
                Title = "Dịch vụ giặt ủi",
                Message = "Cảm ơn bạn đã sử dụng dịch vụ giặt ủi của chúng tôi. Hẹn gặp lại lần sau!",
                Notificationtype = NotificationType.Finish.ToString(),
                Orderid = orderId,
                Createdat = DateTime.UtcNow,
                Ispushenabled = true,
                Isread = false
            };

            await _unitOfWork.Repository<Notification>().InsertAsync(notification);
            await _unitOfWork.SaveChangesAsync();
        }

    }
}
