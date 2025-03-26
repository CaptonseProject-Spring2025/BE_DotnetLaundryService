using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class NotificationResponse
    {
        public Guid NotificationId { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string NotificationType { get; set; }
        public bool IsRead { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? OrderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsPushEnabled { get; set; }
    }

}
