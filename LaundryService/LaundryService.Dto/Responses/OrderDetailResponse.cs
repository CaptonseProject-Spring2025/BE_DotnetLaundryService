using System;

namespace LaundryService.Dto.Responses;

public class OrderDetailResponse
{
 /// <summary>
        /// ID của đơn hàng
        /// </summary>
        public string OrderId { get; set; }
        
        /// <summary>
        /// ID của giao dịch thanh toán
        /// </summary>
        public Guid PaymentId { get; set; }
        
        /// <summary>
        /// Tên khách hàng
        /// </summary>
        public string CustomerName { get; set; }
        
        /// <summary>
        /// Số điện thoại khách hàng
        /// </summary>
        public string CustomerPhone { get; set; }
        
        /// <summary>
        /// Số tiền thanh toán
        /// </summary>
        public decimal Amount { get; set; }
        
        /// <summary>
        /// Ngày thanh toán
        /// </summary>
        public DateTime? PaymentDate { get; set; }
        
        /// <summary>
        /// Trạng thái thanh toán
        /// </summary>
        public string PaymentStatus { get; set; }
        
        /// <summary>
        /// ID giao dịch (nếu là thanh toán trực tuyến)
        /// </summary>
        public string TransactionId { get; set; }
        
        /// <summary>
        /// Trạng thái hiện tại của đơn hàng
        /// </summary>
        public string OrderStatus { get; set; }
        
        /// <summary>
        /// Ngày tạo đơn hàng
        /// </summary>
        public DateTime? CreatedAt { get; set; }
}
