using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class AddressResponse
    {
        public Guid AddressId { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? AddressLabel { get; set; }
        public string DetailAddress { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string? Description { get; set; }
        public DateTime DateCreated { get; set; }
    }
}
