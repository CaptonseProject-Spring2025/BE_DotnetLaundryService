using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class AbsentDriverResponse
    {
        public Guid AbsentId { get; set; }
        public Guid DriverId { get; set; }
        public DateOnly Date { get; set; }
        public TimeSpan From { get; set; }
        public TimeSpan To { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
