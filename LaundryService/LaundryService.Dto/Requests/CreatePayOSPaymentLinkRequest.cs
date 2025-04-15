using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class CreatePayOSPaymentLinkRequest
    {
        [Required(ErrorMessage = "OrderId is required.")]
        public string OrderId { get; set; } = null!;


        [MaxLength(255, ErrorMessage = "Description cannot exceed 255 characters.")]
        public string Description { get; set; } = null!;
    }
}
