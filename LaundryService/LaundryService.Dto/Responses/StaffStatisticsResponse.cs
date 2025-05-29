using System;

namespace LaundryService.Dto.Responses;

public class StaffStatisticsResponse
{
  // Thống kê tổng quan công việc
        public int TotalOrdersProcessed { get; set; }
        public int OrdersInChecking { get; set; }
        public int OrdersInWashing { get; set; }
        public int OrdersInWashed { get; set; }
        public int OrdersQualityChecked { get; set; }
        public int OrdersCompleted { get; set; }
        
        // Hiệu suất làm việc
        public double AverageProcessingTimeHours { get; set; }
        public int OrdersCompletedToday { get; set; }
        public int OrdersCompletedThisWeek { get; set; }
        public int OrdersCompletedThisMonth { get; set; }
        
        // Thống kê theo từng giai đoạn
        public WorkflowStatistics Workflow { get; set; }    = new();
        
        // Thống kê hiệu suất
        public PerformanceStatistics Performance { get; set; } = new();
}
