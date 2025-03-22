using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class AddToCartRequest
    {
        [Required]
        public Guid ServiceDetailId { get; set; }


        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be >= 1")]
        public int Quantity { get; set; } = 1;


        // Danh sách ExtraId kèm theo, có thể rỗng nếu người dùng không chọn Extra nào
        public List<Guid> ExtraIds { get; set; } = new List<Guid>();
    }
}
