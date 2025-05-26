using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class DriverStatisticsResponse
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        public int TotalOrdersCount { get; set; }
        public int CashOrdersCount { get; set; }
        public decimal CashTotalAmount { get; set; }
    }
}
