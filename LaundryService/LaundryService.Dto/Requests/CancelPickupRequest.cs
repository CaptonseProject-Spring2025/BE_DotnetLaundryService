using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class CancelPickupRequest
    {
        public string OrderId { get; set; }
        public string CancelReason { get; set; }
    }
}
