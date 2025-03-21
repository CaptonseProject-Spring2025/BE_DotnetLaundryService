using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class CreateAddressRequest
    {
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, ErrorMessage = "Full name must be at most 100 characters")]
        public string? ContactName { get; set; }


        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Invalid phone number format")]
        public string? ContactPhone { get; set; }


        public string? AddressLabel { get; set; }

        [Required]
        public string DetailAddress { get; set; } = string.Empty;


        [Required]
        public decimal Latitude { get; set; }


        [Required]
        public decimal Longitude { get; set; }

        public string? Description { get; set; }
    }
}
