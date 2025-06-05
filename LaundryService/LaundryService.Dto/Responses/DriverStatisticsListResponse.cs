using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class DriverStatisticsListResponse
    {
        public string OrderId { get; set; } = null!;
        public DateTime CompletedAt { get; set; }
        public string? PaymentName { get; set; }
        public decimal TotalPrice { get; set; }
        public string AssignmentStatus { get; set; }
    }
}
