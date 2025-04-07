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
        Task CreateOrderPlacedNotificationAsync(Guid userId, string orderId);
        Task CreateOrderConfirmedNotificationAsync(Guid userId, string orderId);
        Task CreateOrderCanceledNotificationAsync(Guid userId, string orderId);

    }
}
