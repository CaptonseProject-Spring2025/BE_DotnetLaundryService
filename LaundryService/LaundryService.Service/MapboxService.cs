using LaundryService.Domain.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class MapboxService : IMapboxService
    {
        private readonly HttpClient _httpClient;
        private readonly string _mapboxAccessToken;

        public MapboxService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _mapboxAccessToken = configuration["MapBox:AccessToken"] ?? throw new ArgumentNullException("MapBox AccessToken is missing.");
        }

        public async Task<(decimal Latitude, decimal Longitude)> GetCoordinatesFromAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("Address cannot be empty.");

            var mapboxUrl = $"https://api.mapbox.com/search/geocode/v6/forward?q={Uri.EscapeDataString(address)}&access_token={_mapboxAccessToken}&limit=1";
            var response = await _httpClient.GetStringAsync(mapboxUrl);
            var mapboxData = JsonConvert.DeserializeObject<dynamic>(response);

            if (mapboxData?.features.Count == 0)
                throw new ArgumentException("Invalid address. Please enter a valid location.");

            decimal longitude = (decimal)mapboxData.features[0].geometry.coordinates[0];
            decimal latitude = (decimal)mapboxData.features[0].geometry.coordinates[1];

            return (latitude, longitude);
        }

        public double CalculateDistance(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
        {
            const double EarthRadius = 6371000; // Mét
            var dLat = (double)(lat2 - lat1) * Math.PI / 180.0;
            var dLon = (double)(lon2 - lon1) * Math.PI / 180.0;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos((double)lat1 * Math.PI / 180.0) * Math.Cos((double)lat2 * Math.PI / 180.0) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadius * c;
        }
    }
}
