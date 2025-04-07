using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class AreaOrdersResponse
    {
        public string Area { get; set; } = null!;          // "Area1", "Area2", "Area3"
        public List<ConfirmedOrderInfo> Orders { get; set; } = new();
    }

    public class ConfirmedOrderInfo
    {
        public string OrderId { get; set; } = null!;
        public UserInfoDto UserInfo { get; set; } = new UserInfoDto();
        public string? PickupName { get; set; }
        public string? PickupPhone { get; set; }
        public double Distance { get; set; }
        public string? PickupAddressDetail { get; set; }
        public string? PickupDescription { get; set; }
        public decimal? PickupLatitude { get; set; }
        public decimal? PickupLongitude { get; set; }
        public DateTime? PickupTime { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal? TotalPrice { get; set; }
    }

    public class UserInfoDto
    {
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
