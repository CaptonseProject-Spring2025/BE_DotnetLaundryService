using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class CalculateShippingFeeResponse
    {
        public decimal ShippingFee { get; set; }
        public decimal ApplicableFee { get; set; }
    }
}
