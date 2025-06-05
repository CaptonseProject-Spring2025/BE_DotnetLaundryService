using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class AssignedOrderDetailResponse
    {
        public Guid AssignmentId { get; set; }
        public string OrderId { get; set; }
        public bool? Emergency { get; set; }

        // Thông tin khách hàng
        public string CustomerFullname { get; set; }
        public string CustomerPhone { get; set; }
        public string Address { get; set; }
        public string CurrentStatus { get; set; }
        public decimal? TotalPrice { get; set; }

        // Thông tin tài xế
        public string DriverFullname { get; set; }
        public string DriverPhone { get; set; }

        public DateTime AssignedAt { get; set; }
        public string Status { get; set; }
    }
}
