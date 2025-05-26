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
        public TimeSpan CompletedAt { get; set; }
        public decimal TotalPrice { get; set; }
        public string AssignmentStatus { get; set; }
    }
}
