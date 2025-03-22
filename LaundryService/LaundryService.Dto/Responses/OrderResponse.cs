using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class OrderResponse
    {
        public Guid OrderId { get; set; }
        public string CurrentStatus { get; set; }
        public DateTime? CreatedAt { get; set; }

        public List<OrderItemResponse> Items { get; set; } = new List<OrderItemResponse>();
        // Có thể bổ sung các thông tin khác như tổng giá, phí ship,... tuỳ ý
    }

    public class OrderItemResponse
    {
        public Guid OrderItemId { get; set; }
        public Guid ServiceId { get; set; }
        public string ServiceName { get; set; }
        public int Quantity { get; set; }
        public decimal BasePrice { get; set; }
        public List<OrderExtraResponse> Extras { get; set; } = new List<OrderExtraResponse>();
    }

    public class OrderExtraResponse
    {
        public Guid OrderExtraId { get; set; }
        public Guid ExtraId { get; set; }
        public string ExtraName { get; set; }
        public decimal ExtraPrice { get; set; }
    }
}
