using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Enums
{
    public enum OrderStatusEnum
    {
        InCart,
        Pending,
        Paid,
        Confirmed, //confirmed là chưa trả tiền
        OnDelivery,
        Finished,
        Cancelled
    }
}
