using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class AddAreasRequest
    {
        public string AreaType { get; set; } = null!;
        public List<AreaItem> Areas { get; set; } = new();
    }

    public class AreaItem
    {
        public string Name { get; set; } = null!;
        public List<string> Districts { get; set; } = new();
    }
}
