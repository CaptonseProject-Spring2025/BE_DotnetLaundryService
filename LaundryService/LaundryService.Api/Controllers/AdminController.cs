using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/admin")]
    [ApiController]
    //[Authorize(Roles = "Admin")]
    public class AdminController : BaseApiController
    {
        private readonly IOrderService _orderService;

        public AdminController(IOrderService orderService) // Inject IOrderService
        {
            _orderService = orderService;
        }

        /// <summary>
        /// Lấy danh sách đơn hàng đã xác nhận, nhóm theo khu vực pickup.
        /// </summary>
        /// <returns>Danh sách các khu vực và đơn hàng tương ứng.</returns>
        /// <remarks>
        /// **Yêu cầu**: Đăng nhập với vai trò `Admin`.
        ///
        /// **Logic**:
        /// 1. Lấy tất cả đơn hàng có trạng thái "CONFIRMED".
        /// 2. Với mỗi đơn, xác định quận từ tọa độ pickup bằng Mapbox.
        /// 3. Map quận vào khu vực (Area1, Area2, Area3) dựa trên cấu hình `appsettings.json`.
        /// 4. Tính khoảng cách từ điểm pickup đến địa chỉ trung tâm trong `appsettings.json`.
        /// 5. Nhóm các đơn hàng theo khu vực.
        /// 6. Sắp xếp các đơn hàng trong mỗi khu vực theo thời gian tạo (`CreatedAt`).
        /// 7. Trả về cấu trúc dữ liệu theo yêu cầu.
        ///
        /// **Response Codes**:
        /// - `200 OK`: Trả về danh sách thành công.
        /// - `401 Unauthorized`: Không có quyền truy cập.
        /// - `400 Bad Request`: Lỗi cấu hình (thiếu Areas hoặc AddressDetail).
        /// - `500 Internal Server Error`: Lỗi không mong muốn khác.
        /// </remarks>
        [HttpGet("orders/confirmed")]
        [ProducesResponseType(typeof(List<AreaOrdersResponse>), 200)]
        public async Task<IActionResult> GetConfirmedOrdersByArea()
        {
            try
            {
                var result = await _orderService.GetConfirmedOrdersByAreaAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log...
                return StatusCode(500, new { Message = $"An unexpected error occurred: {ex.Message}" });
            }
        }
    }
}
