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

            data ??= new Dictionary<string, string>();
            data.TryGetValue("orderId", out var orderId);
            var (title, message) = GetNotificationContent(type, orderId);

            foreach (var token in tokens)
            {
                var messageObj = new Message
                {
                    Token = token,
                    Notification = new Notification
                    {
                        Title = title,
                        Body = message
                    },
                    Data = data
                };
                try
                {
                    await FirebaseMessaging.DefaultInstance.SendAsync(messageObj);
                }
                catch (FirebaseMessagingException ex)
                {
                    System.Console.WriteLine($"Failed to send message to token {token}: {ex.Message}");
                    if (ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument ||
                        ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
                    {
                        await _firebaseStorageService.DeleteTokenAsync(userId, token);
                    }
                }
            }
        }

        private (string Title, string Message) GetNotificationContent(NotificationType type, string? orderId = null)
        {
            return type switch
            {
                NotificationType.OrderPlaced => ("Thông báo đặt hàng", "Bạn đã đặt hàng thành công. Vui lòng chờ nhân viên liên hệ."),
                NotificationType.OrderConfirmed => ("Thông báo đặt hàng", "Đơn hàng của bạn đã được xác nhận thành công."),
                NotificationType.OrderCancelled => ("Thông báo đặt hàng", "Đơn hàng của bạn đã được hủy thành công."),
                NotificationType.PickupScheduled => ("Thông báo nhận hàng", "Đơn hàng của bạn đã được lên lịch để tài xế đến nhận. Vui lòng chuẩn bị hàng sẵn sàng để tài xế đến nhận hàng!"),
                NotificationType.PickupStarted => ("Thông báo nhận hàng", "Tài xế đã bắt đầu đi đến nhận đơn hàng của bạn."),
                NotificationType.PickedUp => ("Thông báo nhận hàng", "Tài xế đã nhận đơn hàng thành công."),
                NotificationType.DeliveryScheduled => ("Thông báo giao hàng", "Đơn hàng của bạn đã được lên lịch để tài xế đến giao. Vui lòng có mặt khi tài xế đến giao hàng!"),
                NotificationType.DeliveryStarted => ("Thông báo giao hàng", "Tài xế đã bắt đầu đi giao hàng đến địa chỉ của bạn."),
                NotificationType.Delivered => ("Thông báo giao hàng", "Tài xế đã giao đơn hàng đến bạn thành công."),
                NotificationType.Finish => ("Dịch vụ giặt ủi", "Cảm ơn bạn đã sử dụng dịch vụ giặt ủi của chúng tôi. Hẹn gặp lại lần sau!"),
                NotificationType.AssignedPickup => ("Thông báo nhận hàng", $"Bạn đã được giao đơn nhận hàng {orderId}. Vui lòng kiểm tra và thực hiện nhận hàng."),
                NotificationType.AssignedDelivery => ("Thông báo giao hàng", $"Bạn đã được giao đơn giao hàng {orderId}. Vui lòng kiểm tra và thực hiện hiện giao hàng."),
                NotificationType.PickupArrived => ("Thông báo nhận hàng", "Tài xế đã mang hàng về thành công."),
                _ => ("Thông báo", "Bạn có một thông báo mới.")
            };
        }
    }
}
