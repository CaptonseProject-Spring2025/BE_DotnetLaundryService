using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface INotificationService
    {
        Task CreateOrderPlacedNotificationAsync(Guid userId, Guid orderId);
    }
}
