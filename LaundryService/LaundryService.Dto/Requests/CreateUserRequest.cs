using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class CreateUserRequest
    {
        [Required(ErrorMessage = "FullName is required")]
        [StringLength(100, ErrorMessage = "Full name must be at most 100 characters")]
        public string FullName { get; set; }


        [EmailAddress(ErrorMessage = "Invalid Email format")]
        // Email có thể trống, nếu muốn bắt buộc thì bỏ dấu `?` và thêm Required
        public string? Email { get; set; }


        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
            ErrorMessage = "Password must contain at least one uppercase letter, one digit, and one special character")]
        public string Password { get; set; }


        [Required(ErrorMessage = "Role is required")]
        [RegularExpression(@"^(Admin|Customer|Staff|Driver)$", ErrorMessage = "Role must be 'Admin', 'Customer', 'Driver' or 'Staff'")]
        public string Role { get; set; }


        /// <summary>
        /// Avatar có thể null
        /// </summary>
        public IFormFile? Avatar { get; set; }


        /// <summary>
        /// Ngày sinh có thể bỏ trống
        /// </summary>
        public DateOnly? Dob { get; set; }


        /// <summary>
        /// Nếu muốn optional thì để là string? và bỏ RegularExpression
        /// </summary>
        [Required(ErrorMessage = "Gender is required")]
        [RegularExpression(@"^(Male|Female)$", ErrorMessage = "Gender must be Male or Female")]
        public string Gender { get; set; }


        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Invalid phone number format")]
        public string PhoneNumber { get; set; }


        /// <summary>
        /// RewardPoints có thể trống, mặc định = 0 nếu không nhập
        /// </summary>
        public int? RewardPoints { get; set; }
    }
}
