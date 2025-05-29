using System;

namespace LaundryService.Dto.Responses;

public class WorkflowStatistics
{
   // Checking phase
        public int OrdersCurrentlyChecking { get; set; }
        public int OrdersCheckedToday { get; set; }
        public int OrdersCheckedThisWeek { get; set; }
        
        // Washing phase  
        public int OrdersCurrentlyWashing { get; set; }
        public int OrdersWashedToday { get; set; }
        public int OrdersWashedThisWeek { get; set; }
        
        // Quality check phase
        public int OrdersQualityCheckedToday { get; set; }
        public int OrdersQualityCheckedThisWeek { get; set; }
}
