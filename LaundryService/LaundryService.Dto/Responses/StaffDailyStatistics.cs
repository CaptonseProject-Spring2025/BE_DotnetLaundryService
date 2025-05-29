using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class StaffDailyStatistics
    {
        public DateTime Date { get; set; }
        public int OrdersProcessed { get; set; }
        public int OrdersCompleted { get; set; }
        public double WorkingHours { get; set; }
        public int PhotosUploaded { get; set; }
    }
}