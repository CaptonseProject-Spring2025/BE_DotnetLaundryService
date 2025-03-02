using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class ResetPasswordRequest
    {
        [Required(ErrorMessage = "Phone number is required.")]
        public string PhoneNumber { get; set; }


        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
            ErrorMessage = "Password must contain at least one uppercase letter, one digit, and one special character")]
        public string NewPassword { get; set; }


        [Required(ErrorMessage = "OTP token is required.")]
        public string OtpToken { get; set; }
    }
}
