using FirebaseAdmin.Messaging;
using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class FirebaseNotificationService : IFirebaseNotificationService
    {
        private readonly IFirebaseStorageService _firebaseStorageService;

        public FirebaseNotificationService(IFirebaseStorageService firebaseStorageService)
        {
            _firebaseStorageService = firebaseStorageService;
        }

        public async Task SendOrderNotificationAsync(string userId, NotificationType type, Dictionary<string, string>? data = null)
        {
            var token = await _firebaseStorageService.GetUserFcmTokenAsync(userId);
            if (string.IsNullOrEmpty(token))
                return;

            var (title, message) = GetNotificationContent(type, data?["status"]);

            var messageObj = new Message()
            {
                Token = token,
                Notification = new Notification
                {
                    Title = title,
                    Body = message
                },
                Data = data ?? new Dictionary<string, string>()
            };

            await FirebaseMessaging.DefaultInstance.SendAsync(messageObj);
        }

        private (string Title, string Message) GetNotificationContent(NotificationType type, string? status = null)
        {
            return type switch
            {
                NotificationType.OrderPlaced => ("Thông báo đặt hàng", "Bạn đã đặt hàng thành công. Vui lòng chờ nhân viên liên hệ."),
                NotificationType.OrderConfirmed => ("Xác nhận đơn hàng", "Đơn hàng của bạn đã được xác nhận thành công."),
                _ => ("Thông báo", "Bạn có một thông báo mới.")
            };
        }
    }
}
