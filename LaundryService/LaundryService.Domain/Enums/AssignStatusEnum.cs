using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Enums
{
    public enum AssignStatusEnum
    {
        ASSIGNED_PICKUP,
        PICKING_UP,
        PICKED_UP,
        RECEIVED,
        PICKUP_FAILED,

        ASSIGNED_DELIVERY,
        DELIVERING,
        DELIVERED,
        DELIVERY_FAILED
    }
}
