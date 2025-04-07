using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IMapboxService
    {
        Task<(decimal Latitude, decimal Longitude)> GetCoordinatesFromAddressAsync(string address);

        double CalculateDistance(decimal lat1, decimal lon1, decimal lat2, decimal lon2);

        Task<string?> GetDistrictFromCoordinatesAsync(decimal latitude, decimal longitude);
    }
}
