using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Enums
{
    public enum NotificationType
    {
        OrderPlaced,
        OrderConfirmed,
        OrderCancelled,
        PickupScheduled,
        PickupStarted,
        PickedUp,
        DeliveryScheduled,
        DeliveryStarted,
        Delivered,
        Finish,
        AssignedPickup,
        AssignedDelivery
    }

}
