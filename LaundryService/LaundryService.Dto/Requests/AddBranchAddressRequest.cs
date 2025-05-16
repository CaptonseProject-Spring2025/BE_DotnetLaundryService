using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class AddBranchAddressRequest
    {
        [Required(ErrorMessage = "Branch ID is required.")]
        public string Addressdetail { get; set; }

        [Required(ErrorMessage = "Latitude is required.")]
        public decimal Latitude { get; set; }

        [Required(ErrorMessage = "Longitude is required.")]
        public decimal Longitude { get; set; }
    }
}
