using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class OrderStatusHistoryItemResponse
    {
        public Guid StatusHistoryId { get; set; }
        public string? Status { get; set; }
        public string? StatusDescription { get; set; }
        public string? Notes { get; set; }

        public UpdatedByUser? UpdatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }

        public bool ContainMedia { get; set; } = false;
    }

    public class UpdatedByUser
    {
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
