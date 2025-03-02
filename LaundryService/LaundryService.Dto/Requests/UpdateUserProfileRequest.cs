using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class UpdateUserProfileRequest
    {
        public Guid UserId { get; set; }

        [StringLength(100, ErrorMessage = "Full name must be at most 100 characters")]
        public string? FullName { get; set; }

        public string? Email { get; set; }

        public IFormFile? Avatar { get; set; }

        public DateOnly? Dob { get; set; }

        [RegularExpression(@"^(Male|Female|Other)$", ErrorMessage = "Gender must be Male, Female, or Other")]
        public string? Gender { get; set; }
    }
}
