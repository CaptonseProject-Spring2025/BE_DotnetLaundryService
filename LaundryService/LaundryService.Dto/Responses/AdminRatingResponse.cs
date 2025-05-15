using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class AdminRatingResponse
    {
        public string OrderId { get; set; }
        public string FullName { get; set; }
        public int? Star { get; set; }
        public string Review { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
