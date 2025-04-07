using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace LaundryService.Api.Controllers
{
    [Route("api/mapbox")]
    [ApiController]
    public class MapboxController : ControllerBase
    {
        private readonly IMapboxService _mapboxService;

        public MapboxController(IMapboxService mapboxService)
        {
            _mapboxService = mapboxService;
        }

        /// <summary>
        /// Lấy tọa độ (latitude, longitude) của 1 địa chỉ
        /// </summary>
        /// <param name="address">
        ///     Địa chỉ cần geocoding, ví dụ "1600 Amphitheatre Parkway, Mountain View, CA"
        /// </param>
        /// <returns>
        /// Trả về một object ẩn danh: <c>{ Latitude, Longitude }</c>
        /// </returns>
        /// <remarks>
        /// **Logic**:
        /// 1) Gọi API Mapbox để lấy về tọa độ, giới hạn 1 kết quả.  
        /// 2) Nếu địa chỉ không hợp lệ hoặc bị thiếu => ném <see cref="ArgumentException"/> => 400.  
        /// 3) Nếu kết nối Mapbox thất bại => ném <see cref="HttpRequestException"/> => 500.  
        /// 4) Lỗi không xác định => 500.  
        /// 
        /// **Response codes**:
        /// - **200**: Trả về tọa độ geocoding thành công
        /// - **400**: Địa chỉ không hợp lệ
        /// - **500**: Lỗi kết nối Mapbox hoặc lỗi server khác
        /// </remarks>
        [HttpGet("geocoding")]
        public async Task<IActionResult> GetCoordinates([FromQuery] string address)
        {
            try
            {
                var (latitude, longitude) = await _mapboxService.GetCoordinatesFromAddressAsync(address);
                return Ok(new { Latitude = latitude, Longitude = longitude });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (HttpRequestException)
            {
                return StatusCode(500, new { Message = "Failed to connect to Mapbox API." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Tính khoảng cách giữa 2 điểm địa lý (lat/long) theo bán kính Trái Đất
        /// </summary>
        /// <param name="lat1">Latitude điểm 1</param>
        /// <param name="lon1">Longitude điểm 1</param>
        /// <param name="lat2">Latitude điểm 2</param>
        /// <param name="lon2">Longitude điểm 2</param>
        /// <returns>
        /// Trả về một object ẩn danh: <c>{ Distance }</c>, đơn vị tính bằng mét (double)
        /// </returns>
        /// <remarks>
        /// **Logic**:
        /// 1) Sử dụng công thức Haversine tính quãng đường trên bề mặt địa cầu (đơn vị mét).  
        /// 2) Nếu tham số không hợp lệ => ném <see cref="ArgumentException"/> => 400.  
        /// 3) Lỗi khác => 500.  
        /// 
        /// **Response codes**:
        /// - **200**: Tính thành công, trả về khoảng cách (mét)
        /// - **400**: Dữ liệu truyền vào không hợp lệ
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpGet("calculate-distance")]
        public IActionResult CalculateDistance([FromQuery] decimal lat1, [FromQuery] decimal lon1, [FromQuery] decimal lat2, [FromQuery] decimal lon2)
        {
            try
            {
                double distance = _mapboxService.CalculateDistance(lat1, lon1, lat2, lon2);
                return Ok(new { Distance = distance });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy tên Quận/Huyện (locality) từ tọa độ địa lý (latitude, longitude).
        /// </summary>
        /// <param name="latitude">Vĩ độ (ví dụ: 10.809939).</param>
        /// <param name="longitude">Kinh độ (ví dụ: 106.664737).</param>
        /// <returns>Tên Quận/Huyện nếu tìm thấy, hoặc lỗi nếu không tìm thấy/có lỗi xảy ra.</returns>
        [HttpGet("district")] // Route cụ thể cho action này: api/geocoding/district
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDistrictFromCoordinates(
            [FromQuery][Required] decimal latitude,  // Lấy từ query string và yêu cầu bắt buộc
            [FromQuery][Required] decimal longitude) // Lấy từ query string và yêu cầu bắt buộc
        {
            // Có thể thêm validation phức tạp hơn cho khoảng giá trị của lat/long nếu cần
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var districtName = await _mapboxService.GetDistrictFromCoordinatesAsync(latitude, longitude);

                if (districtName != null)
                {
                    // Trả về tên Quận nếu tìm thấy
                    return Ok(districtName);
                }
                else
                {
                    // Trả về 404 Not Found nếu Mapbox không trả về tên Quận cho tọa độ này
                    return NotFound(new { Message = $"Could not find district for coordinates ({latitude}, {longitude})." });
                }
            }
            catch (Exception ex)
            {
                // Log lỗi ở đây (sử dụng logger thay vì Console.WriteLine trong môi trường production)
                Console.WriteLine($"Error in GetDistrictFromCoordinates: {ex}");
                // Trả về lỗi 500 Internal Server Error cho các lỗi không mong muốn
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An unexpected error occurred while fetching district information." });
            }
        }

    }
}
