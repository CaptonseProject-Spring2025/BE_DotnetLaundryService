using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class ExtraCategoryResponse
    {
        public Guid ExtraCategoryId { get; set; }
        public string Name { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
