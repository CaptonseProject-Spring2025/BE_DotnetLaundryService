using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class UpdateCartItemRequest
    {
        public Guid OrderItemId { get; set; }
        public int Quantity { get; set; }
        public List<Guid>? ExtraIds { get; set; }   // Có thể null/rỗng
    }
}
