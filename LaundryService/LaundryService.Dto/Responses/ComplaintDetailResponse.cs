using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class ComplaintDetailResponse
    {
        public string OrderId { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string PickupAddressDetail { get; set; }
        public string DeliveryAddressDetail { get; set; }
        public DateTime OrderCreatedAt { get; set; }
        public string ComplaintType { get; set; }
        public string ComplaintDescription { get; set; }
        public DateTime CreatedAt { get; set; }
        public string HandlerName { get; set; }
        public string ResolutionDetails { get; set; }
        public DateTime ResolvedAt { get; set; }
    }
}
