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
        PICKEDUP = 5,
        CHECKING = 6,
        CHECKED = 7,
        WASHING = 8,
        WASHED = 9,
        QUALITY_CHECKED = 10,
        SCHEDULED_DELIVERY = 11,
        DELIVERING = 12,
        DELIVERED = 13,
        CANCELLED = 14
    }
}
