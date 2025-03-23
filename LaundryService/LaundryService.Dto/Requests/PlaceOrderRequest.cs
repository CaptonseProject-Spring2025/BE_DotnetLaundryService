using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class PlaceOrderRequest
    {
        [Required]
        public Guid PickupAddressId { get; set; }

        [Required]
        public Guid DeliveryAddressId { get; set; }

        [Required]
        public DateTime Pickuptime { get; set; }

        [Required]
        public DateTime Deliverytime { get; set; }

        // Phí giao hàng
        public decimal Shippingfee { get; set; }

        // Giảm giá ship (thường là số âm nếu trừ tiền, 
        // hoặc số dương nếu logic ngược), 
        // tuỳ theo nghiệp vụ bạn quy ước
        public decimal? Shippingdiscount { get; set; }

        // Phí bổ sung (nếu có)
        public decimal? Applicablefee { get; set; }

        // Giảm giá chung
        public decimal? Discount { get; set; }

        // Tổng tiền client tính
        [Required]
        public decimal Total { get; set; }

        // Note (không bắt buộc)
        public string? Note { get; set; }

        // Thời gian tạo (nếu client không gửi, server sẽ dùng DateTime.Now)
        public DateTime? Createdat { get; set; }
    }
}
