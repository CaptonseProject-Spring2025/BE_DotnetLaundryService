using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class StaffWorkloadStatistics
    {
        public int EmergencyOrders { get; set; }
        public int NormalOrders { get; set; }
        public int OverdueOrders { get; set; }
        public DateTime? NextDeliveryTime { get; set; }
        public int PendingOrders { get; set; }
    }
}