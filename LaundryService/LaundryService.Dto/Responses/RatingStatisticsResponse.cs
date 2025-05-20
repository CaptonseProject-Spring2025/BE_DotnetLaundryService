using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
        public class RatingStatisticsResponse
        {
            public DateTime PeriodStart { get; set; }

            public DateTime PeriodEnd { get; set; }

            public int TotalRatings { get; set; }

            public double AverageStar { get; set; }

            public int TotalReviews { get; set; }
        }
 }

