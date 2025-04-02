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
        CHECKED = 6,
        WASHING = 7,
        WASHED = 8,
        QUALITY_CHECKED = 9,
        SCHEDULED_DELIVERY = 10,
        DELIVERING = 11,
        DELIVERED = 12,
        CANCELLED = 13
    }
}
