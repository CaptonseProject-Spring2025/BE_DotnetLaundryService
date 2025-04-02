using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class OrderDetailCustomResponse
    {
        // Các trường chung của Order
        public Guid OrderId { get; set; }
        public Guid UserId { get; set; }

        public string? PickupLabel { get; set; }
        public string? PickupName { get; set; }
        public string? PickupPhone { get; set; }
        public string? PickupAddressDetail { get; set; }
        public string? PickupDescription { get; set; }
        public decimal? PickupLatitude { get; set; }
        public decimal? PickupLongitude { get; set; }

        public string? DeliveryLabel { get; set; }
        public string? DeliveryName { get; set; }
        public string? DeliveryPhone { get; set; }
        public string? DeliveryAddressDetail { get; set; }
        public string? DeliveryDescription { get; set; }
        public decimal? DeliveryLatitude { get; set; }
        public decimal? DeliveryLongitude { get; set; }

        public DateTime? PickupTime { get; set; }
        public DateTime? DeliveryTime { get; set; }

        // Notes: lấy từ OrderStatusHistory với status = 'PENDING' (nếu có)
        public string? Notes { get; set; }

        // CreatedAt -> chuyển sang giờ Việt Nam
        public DateTime CreatedAt { get; set; }

        // Phần OrderSummary
        public OrderSummaryResponse OrderSummary { get; set; } = new OrderSummaryResponse();

        // Phần CurrentOrderStatus
        public CurrentOrderStatusResponse CurrentOrderStatus { get; set; } = new CurrentOrderStatusResponse();
    }

    public class OrderSummaryResponse
    {
        public List<OrderItemSummary> Items { get; set; } = new List<OrderItemSummary>();
        public decimal EstimatedTotal { get; set; }     // sum(subTotals)
        public decimal? ShippingFee { get; set; }
        public decimal? ShippingDiscount { get; set; }
        public decimal? ApplicableFee { get; set; }
        public decimal? Discount { get; set; }
        public decimal? Otherprice { get; set; }
        public decimal? TotalPrice { get; set; }
    }

    public class OrderItemSummary
    {
        public string ServiceName { get; set; }
        public decimal ServicePrice { get; set; }
        public int Quantity { get; set; }
        public List<ExtraSummary> Extras { get; set; } = new List<ExtraSummary>();
        public decimal SubTotal { get; set; }
    }

    public class ExtraSummary
    {
        public string ExtraName { get; set; }
        public decimal ExtraPrice { get; set; }
    }

    public class CurrentOrderStatusResponse
    {
        public string? CurrentStatus { get; set; }
        public string? StatusDescription { get; set; }
        public DateTime LastUpdate { get; set; }
    }
}
