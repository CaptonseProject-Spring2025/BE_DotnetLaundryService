using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class ComplaintResponse
    {
        public Guid ComplaintId { get; set; }
        public string OrderId { get; set; }
        public string FullName { get; set; }
        public string ComplaintType { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

}
