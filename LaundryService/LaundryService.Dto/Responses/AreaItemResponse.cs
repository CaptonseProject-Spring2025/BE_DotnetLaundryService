using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class AreaItemResponse
    {
        public Guid AreaId { get; set; }
        public string Name { get; set; } = null!;
        public List<string> Districts { get; set; } = new();
    }
}
