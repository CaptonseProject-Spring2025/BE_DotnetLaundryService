using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class InCartOrderAdminResponse
    {
        public string OrderId { get; set; }
        public AdminUserInfo UserInfo { get; set; } = new AdminUserInfo();
        public List<InCartOrderItemResponse> Items { get; set; } = new List<InCartOrderItemResponse>();
        public decimal EstimatedTotal { get; set; }
    }

    public class InCartOrderItemResponse
    {
        public Guid OrderItemId { get; set; }
        public Guid ServiceId { get; set; }
        public string ServiceName { get; set; }
        public decimal ServicePrice { get; set; }
        public int Quantity { get; set; }
        public List<InCartExtraResponse> Extras { get; set; } = new List<InCartExtraResponse>();
        public decimal SubTotal { get; set; }
    }

    public class InCartExtraResponse
    {
        public Guid ExtraId { get; set; }
        public string ExtraName { get; set; }
        public decimal ExtraPrice { get; set; }
    }

    public class AdminUserInfo
    {
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
