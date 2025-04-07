using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class GetDistrictFromCoordinatesRequest
    {
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
    }
}
