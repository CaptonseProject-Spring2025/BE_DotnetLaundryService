using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class StarSummaryResponse
    {
        public int TotalRatings { get; set; }
        public int TotalReviews { get; set; }
        public double AverageStar { get; set; }
    }

}
