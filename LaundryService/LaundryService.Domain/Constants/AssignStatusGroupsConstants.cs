using LaundryService.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Constants
{
    public static class AssignStatusGroupsConstants
    {
        public static readonly HashSet<AssignStatusEnum> Pickup = new()
    {
        AssignStatusEnum.ASSIGNED_PICKUP,
        AssignStatusEnum.PICKING_UP,
        AssignStatusEnum.PICKED_UP,
        AssignStatusEnum.RECEIVED,
        AssignStatusEnum.PICKUP_FAILED
    };

        public static readonly HashSet<AssignStatusEnum> Delivery = new()
    {
        AssignStatusEnum.ASSIGNED_DELIVERY,
        AssignStatusEnum.DELIVERING,
        AssignStatusEnum.DELIVERED,
        AssignStatusEnum.DELIVERY_FAILED
    };
    }
}