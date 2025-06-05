using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class PendingOrdersResponse
    {
        public string OrderId { get; set; }
        public string OrderName { get; set; }
        public int ServiceCount { get; set; }
        public decimal? TotalPrice { get; set; }
        public DateTime? OrderedDate { get; set; }
        public string OrderStatus { get; set; }
        public bool? Emergency { get; set; }
        public Guid? AssignmentId { get; set; }
    }
}
