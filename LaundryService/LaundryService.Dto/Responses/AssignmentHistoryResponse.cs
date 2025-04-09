using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class AssignmentHistoryResponse
    {
        public Guid AssignmentId { get; set; }
        public string OrderId { get; set; } = null!;
        public string? Fullname { get; set; }
        public string? Phonenumber { get; set; }
        public string? Note { get; set; }
        public DateTime? AssignedAt { get; set; }
        public string? Status { get; set; }
        public string? Address { get; set; }
        public string? CurrentStatus { get; set; }
    }


}
