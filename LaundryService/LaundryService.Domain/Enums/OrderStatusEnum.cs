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
        ARRIVED = 7,
        CHECKING = 8,
        CHECKED = 9,
        WASHING = 10,
        WASHED = 11,
        QUALITY_CHECKED = 12,
        SCHEDULED_DELIVERY = 13,
        DELIVERING = 14,

        // Driver đã đến lấy đồ nhưng không thành công, KH hủy or giao hôm khác
        // orderStatushistory vẫn lưu trạng thái, hôm sau Admin giao lại thì quay lại trạng thái SCHEDULED_DELIVERY
        DELIVERYFAILED = 15,

        DELIVERED = 16,
        COMPLETED = 17,
        CANCELLED = 18,
        COMPLAINT = 19
    }
}