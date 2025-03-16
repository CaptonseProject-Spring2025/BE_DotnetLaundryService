using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class AddExtrasToServiceDetailRequest
    {
        [Required(ErrorMessage = "ServiceId is required.")]
        public Guid ServiceId { get; set; }


        [Required(ErrorMessage = "Extras list is required.")]
        [MinLength(1, ErrorMessage = "At least one ExtraId must be provided.")]
        public List<Guid> ExtraIds { get; set; } = new();
    }
}
