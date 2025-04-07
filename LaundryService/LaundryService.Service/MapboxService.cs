using LaundryService.Domain.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        public async Task<string?> GetDistrictFromCoordinatesAsync(decimal latitude, decimal longitude)
        {
            // Sử dụng CultureInfo.InvariantCulture để đảm bảo dấu '.' được dùng làm dấu thập phân trong URL
            var latitudeString = latitude.ToString(CultureInfo.InvariantCulture);
            var longitudeString = longitude.ToString(CultureInfo.InvariantCulture);

            // Xây dựng URL cho Mapbox Reverse Geocoding API
            // Chúng ta chỉ cần lấy thông tin 'locality' (thường tương ứng với Quận/Huyện ở VN)
            // Thêm types=locality để giới hạn kết quả (tùy chọn, nhưng có thể tối ưu)
            var mapboxUrl = $"https://api.mapbox.com/search/geocode/v6/reverse?longitude={longitudeString}&latitude={latitudeString}&access_token={_mapboxAccessToken}&types=locality&limit=1";

            try
            {
                var response = await _httpClient.GetStringAsync(mapboxUrl);
                var mapboxData = JsonConvert.DeserializeObject<dynamic>(response);

                // Kiểm tra cấu trúc JSON trả về dựa trên ví dụ bạn cung cấp
                // Cần tìm features[0].properties.context.locality.name
                // Hoặc nếu dùng types=locality thì có thể là features[0].properties.name

                if (mapboxData?.features != null && mapboxData.features.Count > 0)
                {
                    var feature = mapboxData.features[0];
                    // Ưu tiên lấy từ context nếu có (cấu trúc chi tiết hơn)
                    if (feature.properties?.context?.locality?.name != null)
                    {
                        return (string)feature.properties.context.locality.name;
                    }
                    // Nếu không có context.locality (ví dụ khi dùng types=locality), thử lấy trực tiếp name
                    else if (feature.properties?.name != null && feature.properties?.feature_type == "locality") // Kiểm tra feature_type để chắc chắn hơn
                    {
                        return (string)feature.properties.name;
                    }
                    // Hoặc kiểm tra cấu trúc response khi chỉ có 1 feature và feature đó là locality
                    else if (feature.properties?.name != null && feature.properties?.mapbox_id?.ToString().StartsWith("locality.") == true)
                    {
                        return (string)feature.properties.name;
                    }
                }

                // Nếu không tìm thấy thông tin Quận
                return null;
            }
            catch (HttpRequestException ex)
            {
                // Log lỗi ở đây (ví dụ: không kết nối được Mapbox, lỗi API key,...)
                Console.WriteLine($"Error calling Mapbox API: {ex.Message}");
                // Có thể throw một exception cụ thể hơn hoặc trả về null tùy theo yêu cầu xử lý lỗi
                return null;
            }
            catch (Exception ex) // Bắt các lỗi khác (ví dụ: lỗi parsing JSON)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                return null;
            }
        }
    }
}
