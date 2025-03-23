using FirebaseAdmin.Messaging;
using LaundryService.Domain.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class FirebaseNotificationService : IFirebaseNotificationService
    {
        public async Task SendFirebaseNotificationAsync(string token, string title, string message, Dictionary<string, string>? data = null)
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

            await FirebaseMessaging.DefaultInstance.SendAsync(messageObj);
        }
    }
}