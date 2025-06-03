using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class AddOtherPriceRequest
    {
        [Required (ErrorMessage = "otherPrice is required.")]
        public decimal otherPrice { get; set; }

        [Required (ErrorMessage = "otherPriceNote is required.")]
        public string otherPriceNote { get; set; }
    }
}
