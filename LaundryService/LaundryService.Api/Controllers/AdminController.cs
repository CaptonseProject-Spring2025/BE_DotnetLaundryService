using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : BaseApiController
    {
        private readonly IOrderService _orderService;
        private readonly IFirebaseNotificationService _firebaseNotificationService;
        private readonly INotificationService _notificationService;

        public AdminController(IOrderService orderService,IFirebaseNotificationService firebaseNotificationService, INotificationService notificationService) // Inject IOrderService
        {
            _orderService = orderService;
            _firebaseNotificationService = firebaseNotificationService;
            _notificationService = notificationService;
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
                return StatusCode(500, new { Message = $"An unexpected error occurred: {ex.Message}" });
            }
        }

        /// <summary>
        /// Giao việc pickup cho 1 driver, áp dụng cho danh sách orderId.
        /// </summary>
        /// <param name="request">
        ///     - <c>DriverId</c>
        ///     - <c>OrderIds</c> (danh sách order)
        /// </param>
        /// <remarks>
        /// **Mục đích**: Admin gán driver đến lấy các đơn hàng.
        ///
        /// **Logic**:
        /// Kiểm tra từng orderId => phải ở trạng thái "CONFIRMED"
        /// 
        /// **Response codes**:
        /// - 200: Giao việc thành công
        /// - 400: Lỗi logic (order không đúng trạng thái, driverId rỗng,...)
        /// - 404: Order không tồn tại
        /// - 401: Chưa đăng nhập hoặc không phải admin
        /// - 500: Lỗi khác
        /// </remarks>
        [HttpPost("assign-pickup")]
        public async Task<IActionResult> AssignPickupToDriver([FromBody] AssignPickupRequest request)
        {
            try
            {
                await _orderService.AssignPickupToDriverAsync(HttpContext, request);

                var orderId = request.OrderIds.First();
                var customerId = await _orderService.GetCustomerIdByOrderAsync(orderId);

                try
                {
                    await _notificationService.CreatePickupScheduledNotificationAsync(customerId, orderId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi tạo notification trong hệ thống: {ex.Message}");
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _firebaseNotificationService.SendOrderNotificationAsync(
                            customerId.ToString(),
                            NotificationType.PickupScheduled
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi gửi thông báo: {ex.Message}");
                    }
                });

                return Ok(new { Message = "Giao việc lấy hàng thành công!" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }
    }
}
