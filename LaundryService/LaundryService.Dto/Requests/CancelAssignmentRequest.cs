using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public sealed class CancelAssignmentRequest
    {
        public List<Guid> AssignmentIds { get; set; } = new();
    }
}
