using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class AssignPickupRequest
    {
        [Required(ErrorMessage = "Danh sách OrderId không được để trống.")]
        [MinLength(1, ErrorMessage = "Phải chọn ít nhất một OrderId.")]
        public List<string> OrderIds { get; set; } = new List<string>();


        [Required(ErrorMessage = "DriverId không được để trống.")]
        public Guid DriverId { get; set; }
    }
}
