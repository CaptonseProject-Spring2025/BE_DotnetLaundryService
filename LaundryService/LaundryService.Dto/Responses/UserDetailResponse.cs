using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class UserDetailResponse
    {
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public string? Status { get; set; }
        public string? Avatar { get; set; }
        public DateOnly? Dob { get; set; }
        public string? Gender { get; set; }
        public int RewardPoints { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateModified { get; set; }
    }
}
