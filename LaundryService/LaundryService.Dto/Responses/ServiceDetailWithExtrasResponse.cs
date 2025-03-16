using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class ServiceDetailWithExtrasResponse
    {
        public Guid ServiceId { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<ExtraCategoryWithExtrasResponse> ExtraCategories { get; set; } = new();
    }

    public class ExtraCategoryWithExtrasResponse
    {
        public Guid ExtraCategoryId { get; set; }
        public string? CategoryName { get; set; }
        public List<ExtraResponse> Extras { get; set; } = new();
    }
}
