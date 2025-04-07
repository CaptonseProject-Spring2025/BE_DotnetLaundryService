using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class CartResponse
    {
        public string OrderId { get; set; }
        public List<CartItemResponse> Items { get; set; } = new List<CartItemResponse>();

        // Tổng tạm tính của cả giỏ
        public decimal EstimatedTotal { get; set; }
    }

    public class CartItemResponse
    {
        public Guid OrderItemId { get; set; }

        // ID, Tên & Giá service detail
        public Guid ServiceId { get; set; }
        public string ServiceName { get; set; }
        public decimal ServicePrice { get; set; }

        public int Quantity { get; set; }

        public List<CartExtraResponse> Extras { get; set; } = new List<CartExtraResponse>();

        // Tổng tiền của Item = (ServicePrice + sum(ExtraPrice)) * Quantity
        public decimal SubTotal { get; set; }
    }

    public class CartExtraResponse
    {
        public Guid ExtraId { get; set; }
        public string ExtraName { get; set; }
        public decimal ExtraPrice { get; set; }
    }
}
