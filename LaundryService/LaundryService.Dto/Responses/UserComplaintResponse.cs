using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class UserComplaintResponse
    {
        public Guid ComplaintId { get; set; }
        public string OrderId { get; set; }
        public string ComplaintType { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

}
