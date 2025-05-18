using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class CalculateShippingFeeRequest
    {
        [Required (ErrorMessage = "Pickup address is required.")]
        public Guid PickupAddressId { get; set; }

        [Required (ErrorMessage = "Delivery address is required.")]
        public Guid DeliveryAddressId { get; set; }

        [Required (ErrorMessage = "Pickup time is required.")]
        public DateTime PickupTime { get; set; }

        [Required (ErrorMessage = "Delivery time is required.")]
        public DateTime DeliveryTime { get; set; }

        [Required (ErrorMessage = "Service Name is required.")]
        public string ServiceName { get; set; } = null!;

        [Required (ErrorMessage = "MinCompleteTime is required.")]
        /// <summary>Đơn vị: giờ.</summary>
        public int MinCompleteTime { get; set; }

        [Required (ErrorMessage = "EstimatedTotal is required.")]
        public decimal EstimatedTotal { get; set; }
    }
}
