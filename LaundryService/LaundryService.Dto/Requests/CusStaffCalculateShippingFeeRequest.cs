using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class CusStaffCalculateShippingFeeRequest
    {
        public Guid? DeliveryAddressId { get; set; }
        public DateTime DeliveryTime { get; set; }

        [Required(ErrorMessage = "Service Name is required.")]
        public string ServiceName { get; set; } = null!;

        [Required(ErrorMessage = "MinCompleteTime is required.")]
        /// <summary>Đơn vị: giờ.</summary>
        public int MinCompleteTime { get; set; }

        [Required(ErrorMessage = "EstimatedTotal is required.")]
        public decimal EstimatedTotal { get; set; }
    }
}
