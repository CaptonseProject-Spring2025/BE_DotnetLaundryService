using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IFirebaseNotificationService
    {
        Task SendFirebaseNotificationAsync(string token, string title, string message, Dictionary<string, string>? data = null);
    }
}
