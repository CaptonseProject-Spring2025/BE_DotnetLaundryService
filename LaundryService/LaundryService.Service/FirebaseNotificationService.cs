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
            var tokens = await _firebaseStorageService.GetUserFcmTokensAsync(userId);
            if (tokens == null || tokens.Count == 0)
                return;

            var (title, message) = GetNotificationContent(type, data?["status"]);

            foreach (var token in tokens)
            {
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

                try
                {
                    await FirebaseMessaging.DefaultInstance.SendAsync(messageObj);
                }
                catch (FirebaseMessagingException ex)
                {
                    Console.WriteLine($"Failed to send message to token {token}: {ex.Message}");

                    // Optional: remove invalid tokens
                    if (ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument ||
                        ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
                    {
                        await _firebaseStorageService.DeleteTokenAsync(userId, token);
                        Console.WriteLine($"Deleted invalid token: {token}");
                    }
                }
            }
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
