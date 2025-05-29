using System;

namespace LaundryService.Dto.Responses;

public class PerformanceStatistics
{
   public double ProductivityScore { get; set; }
        public double AverageCheckingTimeHours { get; set; }
        public double AverageWashingTimeHours { get; set; }
        public int PhotosUploadedToday { get; set; }
        public int PhotosUploadedThisWeek { get; set; }
        public int TotalPhotosUploaded { get; set; }
        public int OrdersWithIssues { get; set; }
        public double QualityRating { get; set; }
}
