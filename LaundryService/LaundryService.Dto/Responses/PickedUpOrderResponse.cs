using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class PickedUpOrderResponse
    {
        public string OrderId { get; set; }
        public CustomerInfoDto CustomerInfo { get; set; } = new CustomerInfoDto();
        public string ServiceNames { get; set; } = string.Empty;
        public int ServiceCount { get; set; }
        public DateTime? OrderDate { get; set; }
        public DateTime? PickupTime { get; set; }
        public DateTime? DeliveryTime { get; set; }
        public string CurrentStatus { get; set; } = string.Empty;
        public decimal? TotalPrice { get; set; }
        public bool? Emergency { get; set; }
    }

    public class CustomerInfoDto
    {
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
    }
}
