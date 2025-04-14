using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class ReorderRequest
    {
        [Required]
        public string OrderId { get; set; } = null!;
    }
}
