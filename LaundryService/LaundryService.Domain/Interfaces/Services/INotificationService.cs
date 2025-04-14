using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface INotificationService
    {
        Task<IEnumerable<NotificationResponse>> GetNotificationsByUserIdAsync(HttpContext httpContext);
        Task DeleteNotificationAsync(Guid notificationId);
        Task MarkAsReadAsync(Guid notificationId);
        Task MarkAllUserNotificationsAsReadAsync(HttpContext httpContext);
        Task DeleteAllNotificationsOfCurrentUserAsync(HttpContext context);
        Task CreateOrderPlacedNotificationAsync(Guid userId, string orderId);
        Task CreateOrderConfirmedNotificationAsync(Guid userId, string orderId);
        Task CreateOrderCanceledNotificationAsync(Guid userId, string orderId);
        Task CreatePickupScheduledNotificationAsync(Guid customerId, string orderId);
        Task CreatePickupStartedNotificationAsync(Guid userId, string orderId);
        Task CreateOrderPickedUpNotificationAsync(Guid userId, string orderId);
        Task CreateDeliveryStartedNotificationAsync(Guid userId, string orderId);
        Task CreateOrderDeliveredNotificationAsync(Guid userId, string orderId);
        Task CreateThankYouNotificationAsync(Guid userId, string orderId);

    }
}
