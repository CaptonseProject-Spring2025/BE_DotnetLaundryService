using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class DriverCashOrderResponse
    {
        public Guid PaymentId { get; set; }
        public string OrderId { get; set; } = null!;
        public decimal Amount { get; set; }

        public DateTime? AssignedAt { get; set; }
        public DateTime? PaymentDate { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsReturnedToAdmin { get; set; }
    }
}
