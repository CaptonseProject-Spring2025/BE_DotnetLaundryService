using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class CategoryDetailResponse
    {
        public Guid CategoryId { get; set; }
        public string Name { get; set; }
        public string? Icon { get; set; }
        public List<SubCategory> SubCategories { get; set; } = new();
    }

    public class SubCategory
    {
        public Guid SubCategoryId { get; set; }
        public string Name { get; set; }
        public List<ServiceDetailResponse> ServiceDetails { get; set; } = new();
    }
}
