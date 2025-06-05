using LaundryService.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IFirebaseNotificationService
    {
        Task SendOrderNotificationAsync(string userId, NotificationType type, string? orderId = null, Dictionary<string, string>? data = null);
    }
}
