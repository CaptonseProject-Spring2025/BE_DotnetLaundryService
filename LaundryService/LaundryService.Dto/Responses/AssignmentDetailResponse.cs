using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class AssignmentDetailResponse
    {
        public Guid AssignmentId { get; set; }
        public string OrderId { get; set; } = null!;
        public Guid CustomerId { get; set; }
        public string? Fullname { get; set; }
        public string? Phonenumber { get; set; }
        public string? Note { get; set; }
        public DateTime? AssignedAt { get; set; }
        public string? Status { get; set; }
        public string? PickupAddress { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? PickupDescription { get; set; }
        public string? DeliveryDescription { get; set; }
        public decimal? TotalPrice { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? CurrentStatus { get; set; }
    }

}
