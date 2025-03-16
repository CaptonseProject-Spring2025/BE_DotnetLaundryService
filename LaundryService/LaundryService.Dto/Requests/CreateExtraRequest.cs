using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class CreateExtraRequest
    {
        [Required(ErrorMessage = "Extra category ID is required.")]
        public Guid ExtraCategoryId { get; set; }


        [Required(ErrorMessage = "Name is required.")]
        public string Name { get; set; }


        public string? Description { get; set; }


        [Required(ErrorMessage = "Price is required.")]
        [Range(0, double.MaxValue, ErrorMessage = "Price must be a positive number.")]
        public decimal Price { get; set; }


        public IFormFile? Image { get; set; }
    }
}
