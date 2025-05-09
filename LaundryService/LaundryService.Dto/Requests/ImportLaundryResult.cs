using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public sealed record ImportLaundryResult
    {
        public int CategoriesInserted { get; set; }
        public int SubCategoriesInserted { get; set; }
        public int ServicesInserted { get; set; }
        public int ExtrasInserted { get; set; }
        public int ServiceExtraMapped { get; set; }
        public List<string> ErrorRows { get; init; } = new();
    }
}
