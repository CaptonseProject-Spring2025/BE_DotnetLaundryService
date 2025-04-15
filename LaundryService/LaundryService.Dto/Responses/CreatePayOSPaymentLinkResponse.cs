using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class CreatePayOSPaymentLinkResponse
    {
        public Guid PaymentId { get; set; }         // paymentid
        public string CheckoutUrl { get; set; } = null!;  // data.checkoutUrl
        public string QrCode { get; set; } = null!;       // data.qrCode
        public string PaymentLinkId { get; set; } = null!;// data.paymentLinkId
        public string Status { get; set; } = null!;       // data.status
    }
}
