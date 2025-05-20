using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Enums
{
    public enum OrderStatusEnum
    {
        INCART = 0,
        PENDING = 1,
        CONFIRMED = 2,
        SCHEDULED_PICKUP = 3,
        PICKINGUP = 4,

        // Driver đã đến lấy đồ nhưng không thành công, KH hủy or giao hôm khác
        // orderStatushistory vẫn lưu trạng thái, hôm sau Admin giao lại thì quay lại trạng thái SCHEDULED_PICKUP
        PICKUPFAILED = 5, 

        PICKEDUP = 6,
        CHECKING = 7,
        CHECKED = 8,
        WASHING = 9,
        WASHED = 10,
        QUALITY_CHECKED = 11,
        SCHEDULED_DELIVERY = 12,
        DELIVERING = 13,

        // Driver đã đến lấy đồ nhưng không thành công, KH hủy or giao hôm khác
        // orderStatushistory vẫn lưu trạng thái, hôm sau Admin giao lại thì quay lại trạng thái SCHEDULED_DELIVERY
        DELIVERYFAILED = 14,
        
        DELIVERED = 15,
        COMPLETED = 16,
        CANCELLED = 17,
        COMPLAINT = 18
    }
}