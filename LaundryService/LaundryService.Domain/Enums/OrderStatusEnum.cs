using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Enums
{
    public enum OrderStatusEnum
    {
        INCART,
        PENDING,
        CONFIRMED,
        SCHEDULED_PICKUP,
        PICKINGUP,
        PICKEDUP,
        CHECKED,
        WASHING,
        WASHED,
        QUALITY_CHECKED,
        SCHEDULED_DELIVERY,
        DELIVERING,
        DELIVERED,
        CANCELLED
    }
}
