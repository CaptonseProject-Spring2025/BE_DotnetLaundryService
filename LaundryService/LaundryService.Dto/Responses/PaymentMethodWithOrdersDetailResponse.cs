using System;

namespace LaundryService.Dto.Responses;

public class PaymentMethodWithOrdersDetailResponse
{
// <summary>
        /// ID của phương thức thanh toán
        /// </summary>
        public Guid PaymentMethodId { get; set; }
        
        /// <summary>
        /// Tên phương thức thanh toán
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Mô tả phương thức thanh toán
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Trạng thái hoạt động
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Thời gian tạo
        /// </summary>
        public DateTime? CreatedAt { get; set; }
        
        /// <summary>
        /// Số lượng đơn hàng đã sử dụng phương thức thanh toán này
        /// </summary>
        public int OrderCount { get; set; }
        
        /// <summary>
        /// Tổng doanh thu từ phương thức thanh toán này
        /// </summary>
        public decimal TotalRevenue { get; set; }
        
        /// <summary>
        /// Danh sách chi tiết các đơn hàng
        /// </summary>
        public List<OrderDetailResponse> OrderDetails { get; set; }
}
