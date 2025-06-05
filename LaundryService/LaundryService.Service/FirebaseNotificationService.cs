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

        public async Task SendOrderNotificationAsync(
            string userId,
            NotificationType type,
            string? orderId = null,
            Dictionary<string, string>? data = null)
        {
            var tokens = await _firebaseStorageService.GetUserFcmTokensAsync(userId);
            if (tokens is null || tokens.Count == 0) return;

            data ??= new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(orderId))
                data["orderId"] = orderId;

            var (title, body) = GetNotificationContent(type);

            var sendTasks = tokens.Select(async token =>
            {
                var msg = new Message
                {
                    Token = token,
                    Notification = new Notification { Title = title, Body = body },
                    Data = data
                };

                try
                {
                    await FirebaseMessaging.DefaultInstance.SendAsync(msg);
                }
                catch (FirebaseMessagingException ex)
                {
                    System.Console.WriteLine($"Failed to send message to token {token}: {ex.Message}");
                    if (ex.MessagingErrorCode is MessagingErrorCode.InvalidArgument
                        or MessagingErrorCode.Unregistered)
                    {
                        await _firebaseStorageService.DeleteTokenAsync(userId, token);
                    }
                }
            });

            await Task.WhenAll(sendTasks);
        }

        private static (string Title, string Message) GetNotificationContent(NotificationType type) =>
            type switch
            {
                NotificationType.OrderPlaced => ("Thông báo đặt hàng", "Bạn đã đặt hàng thành công. Vui lòng chờ nhân viên liên hệ."),
                NotificationType.OrderConfirmed => ("Thông báo đặt hàng", "Đơn hàng của bạn đã được xác nhận thành công."),
                NotificationType.OrderCancelled => ("Thông báo đặt hàng", "Đơn hàng của bạn đã được hủy thành công."),
                NotificationType.PickupScheduled => ("Thông báo nhận hàng", "Đơn hàng của bạn đã được lên lịch để tài xế đến nhận. Vui lòng chuẩn bị hàng sẵn sàng."),
                NotificationType.PickupStarted => ("Thông báo nhận hàng", "Tài xế đang trên đường đến nhận đơn hàng của bạn."),
                NotificationType.PickedUp => ("Thông báo nhận hàng", "Tài xế đã nhận đơn hàng thành công."),
                NotificationType.DeliveryScheduled => ("Thông báo giao hàng", "Đơn hàng của bạn đã được lên lịch giao. Vui lòng có mặt khi tài xế đến giao!"),
                NotificationType.DeliveryStarted => ("Thông báo giao hàng", "Tài xế đang trên đường giao hàng đến bạn."),
                NotificationType.Delivered => ("Thông báo giao hàng", "Tài xế đã giao đơn hàng thành công."),
                NotificationType.Finish => ("Dịch vụ giặt ủi", "Cảm ơn bạn đã sử dụng dịch vụ giặt ủi của chúng tôi. Hẹn gặp lại!"),
                NotificationType.AssignedPickup => ("Thông báo nhận hàng", "Bạn vừa được giao một đơn nhận hàng mới. Vui lòng kiểm tra và thực hiện."),
                NotificationType.AssignedDelivery => ("Thông báo giao hàng", "Bạn vừa được giao một đơn giao hàng mới. Vui lòng kiểm tra và thực hiện."),
                _ => ("Thông báo", "Bạn có một thông báo mới.")
            };
    }
}