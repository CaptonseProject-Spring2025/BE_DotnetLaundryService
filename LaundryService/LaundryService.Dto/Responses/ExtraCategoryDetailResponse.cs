using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class ExtraCategoryDetailResponse
    {
        public Guid ExtraCategoryId { get; set; }

        public string Name { get; set; }

        public DateTime? CreatedAt { get; set; }

        public List<ExtraResponse> Extras { get; set; } = new();
    }
}
