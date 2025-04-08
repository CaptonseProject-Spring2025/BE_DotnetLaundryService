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
        AssignStatusEnum.PICKUP_SUCCESS,
        AssignStatusEnum.PICKUP_FAILED
    };

        public static readonly HashSet<AssignStatusEnum> Delivery = new()
    {
        AssignStatusEnum.ASSIGNED_DELIVERY,
        AssignStatusEnum.DELIVERY_SUCCESS,
        AssignStatusEnum.DELIVERY_FAILED
    };
    }
}