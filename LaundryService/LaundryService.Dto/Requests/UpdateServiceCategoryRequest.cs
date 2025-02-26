using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class UpdateServiceCategoryRequest
    {
        public string? Name { get; set; }

        public IFormFile? Icon { get; set; }
    }
}
