using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Enums
{
    public enum AssignStatusEnum
    {
        PROCESING,
        FAIL,
        SUCCESS,

        //pickup
        ASSIGNED_PICKUP,
        PICKING_UP,
        PICKED_UP,
        RECEIVED,
        CANCELLED_ASSIGNED_PICKUP,

        ASSIGNED_DELIVERY,
        DELIVERING,
        DELIVERED,
        FINISH,
        CANCELLED_ASSIGNED_DELIVERY
    }
}
