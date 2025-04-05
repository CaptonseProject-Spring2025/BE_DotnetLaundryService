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

        /// <summary>
        /// Lấy danh sách thông báo cho user từ JWT
        /// </summary>
        public async Task<IEnumerable<NotificationResponse>> GetNotificationsByUserIdAsync(HttpContext httpContext)
        {
            var userId = _util.GetCurrentUserIdOrThrow(httpContext); // Lấy userId từ JWT

            var notifications = await _unitOfWork.Repository<Notification>()
                .GetAllAsync(n => n.Userid == userId);

            var sortedNotifications = notifications
        .OrderByDescending(n => n.Createdat ?? DateTime.MinValue) // Sort giảm dần theo thời gian
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


        /// <summary>
        /// Xóa một thông báo
        /// </summary>
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

        /// <summary>
        /// Đánh dấu thông báo là đã đọc
        /// </summary>
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

        public async Task CreateOrderPlacedNotificationAsync(Guid userId, Guid orderId)
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

        public async Task CreateOrderConfirmedNotificationAsync(Guid userId, Guid orderId)
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
    }
}
