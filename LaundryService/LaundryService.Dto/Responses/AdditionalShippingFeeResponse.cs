using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class AdditionalShippingFeeResponse
    {
        public int PickupFailCount { get; set; }
        public decimal PickupFailFee { get; set; }

        public int DeliveryFailCount { get; set; }
        public decimal DeliveryFailFee { get; set; }

        public decimal Total => PickupFailFee + DeliveryFailFee;
    }
}
