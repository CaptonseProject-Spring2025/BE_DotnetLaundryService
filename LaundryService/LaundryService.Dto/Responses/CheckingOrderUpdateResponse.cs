using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class CheckingOrderUpdateResponse
    {
        public Guid? Statushistoryid { get; set; }
        public string OrderId { get; set; }
        public string? Notes { get; set; }
        public bool? IsFail { get; set; }
        public List<PhotoInfo> PhotoUrls { get; set; } = new List<PhotoInfo>();
    }

    public class PhotoInfo
    {
        public string PhotoUrl { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
    }
}
