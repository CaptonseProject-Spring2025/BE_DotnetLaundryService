using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class UserComplaintDetailResponse
    {
        public string OrderId { get; set; }
        public string ComplaintType { get; set; }
        public string ComplaintDescription { get; set; }
        public string Status { get; set; }
        public string ResolutionDetails { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ResolvedAt { get; set; }
    }
}
