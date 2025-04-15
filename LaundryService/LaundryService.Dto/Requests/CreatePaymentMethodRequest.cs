using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class CreatePaymentMethodRequest
    {
        public string Name { get; set; }          // Bắt buộc
        public string? Description { get; set; }  // Tùy chọn
        public bool? IsActive { get; set; }
    }
}
