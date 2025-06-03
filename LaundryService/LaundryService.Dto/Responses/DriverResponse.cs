using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class DriverResponse
    {
        public Guid UserId { get; set; }
        public string Fullname { get; set; }
        public string Role { get; set; }
        public string Status { get; set; }
        public string Avatar { get; set; }
        public DateOnly? Dob { get; set; }
        public string Gender { get; set; }
        public string PhoneNumber { get; set; }
        public int DeliveryOrderCount { get; set; }
        public int PickupOrderCount { get; set; }
        public int CurrentOrderCount { get; set; }
    }
}
