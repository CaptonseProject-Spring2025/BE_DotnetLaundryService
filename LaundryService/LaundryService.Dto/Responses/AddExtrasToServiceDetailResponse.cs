using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class AddExtrasToServiceDetailResponse
    {
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public List<Guid> FailedExtras { get; set; } = new();
    }
}
