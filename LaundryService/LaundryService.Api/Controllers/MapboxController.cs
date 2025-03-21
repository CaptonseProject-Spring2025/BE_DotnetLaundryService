using LaundryService.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
    }
}
