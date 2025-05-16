using LaundryService.Domain.Entities;
using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using LaundryService.Service;
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
        private readonly IAdminService _adminService;
        private readonly IAreaService _areaService;
        private readonly IFirebaseNotificationService _firebaseNotificationService;
        private readonly INotificationService _notificationService;

        public AdminController(IAdminService adminService, IFirebaseNotificationService firebaseNotificationService, INotificationService notificationService, IAreaService areaService) // Inject IOrderService
        {
            _adminService = adminService;
            _firebaseNotificationService = firebaseNotificationService;
            _notificationService = notificationService;
            _areaService = areaService;
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
                var result = await _adminService.GetConfirmedOrdersByAreaAsync();
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
                await _adminService.AssignPickupToDriverAsync(HttpContext, request);

                var driverId = request.DriverId;

                foreach (var orderId in request.OrderIds)
                {
                    var customerId = await _adminService.GetCustomerIdByOrderAsync(orderId);

                    await _notificationService.CreatePickupScheduledNotificationAsync(customerId, orderId);

                    _ = Task.Run(async () =>
                    {
                        await _firebaseNotificationService.SendOrderNotificationAsync(
                            customerId.ToString(),
                            NotificationType.PickupScheduled,
                            new Dictionary<string, string> { ["orderId"] = orderId }
                        );
                    });
                }

                foreach (var orderId in request.OrderIds)
                {
                    await _notificationService.CreateAssignedPickupNotificationAsync(driverId, orderId);

                    _ = Task.Run(async () =>
                    {
                        await _firebaseNotificationService.SendOrderNotificationAsync(
                            driverId.ToString(),
                            NotificationType.AssignedPickup,
                            new Dictionary<string, string> { ["orderId"] = orderId }
                        );
                    });
                }


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

        /// <summary>
        /// Lấy danh sách đơn hàng ở trạng thái QUALITY_CHECKED, nhóm theo khu vực, để chuẩn bị giao hàng.
        /// </summary>
        /// <remarks>
        /// **Logic**:
        /// 1) Lọc Order `Currentstatus = QUALITY_CHECKED`
        /// 2) Gọi Mapbox để tìm quận => phân vào khu vực (Area1, Area2, Area3 hoặc Unknown)
        /// 3) Trong cùng 1 khu vực, sắp xếp các đơn theo CreatedAt
        /// 
        /// **Response codes**:
        /// - `200 OK`: Trả về danh sách
        /// - `401 Unauthorized`: Không có quyền (Admin)
        /// - `500 Internal Server Error`: Lỗi server
        /// </remarks>
        [HttpGet("orders/quality-checked")]
        [ProducesResponseType(typeof(List<AreaOrdersResponse>), 200)]
        public async Task<IActionResult> GetQualityCheckedOrdersByArea()
        {
            try
            {
                var result = await _adminService.GetQualityCheckedOrdersByAreaAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log ra file nếu cần
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Giao việc giao hàng cho tài xế (Driver).
        /// </summary>
        /// <param name="request">Danh sách OrderId + DriverId</param>
        /// <remarks>
        /// **Logic**:
        /// 1) Kiểm tra driverId có hợp lệ (tồn tại, role=Driver).
        /// 2) Với mỗi orderId:
        ///    - Tạo 1 record `Orderassignmenthistory` (Status="ASSIGNED_DELIVERY")
        ///    - Tạo 1 record `Orderstatushistory` (Status="SCHEDULED_DELIVERY")
        ///    - Update Order.Currentstatus = "SCHEDULED_DELIVERY"
        ///
        /// **Response codes**:
        /// - `200`: Giao việc thành công.
        /// - `400`: Dữ liệu không hợp lệ (driver rỗng, orderIds rỗng,...).
        /// - `404`: Không tìm thấy driver hoặc không tìm thấy order.
        /// - `500`: Lỗi server.
        /// </remarks>
        [HttpPost("assign-delivery")]
        public async Task<IActionResult> AssignDeliveryToDriver([FromBody] AssignPickupRequest request)
        {
            try
            {
                await _adminService.AssignDeliveryToDriverAsync(HttpContext, request);

                var driverId = request.DriverId;

                foreach (var orderId in request.OrderIds)
                {
                    var customerId = await _adminService.GetCustomerIdByOrderAsync(orderId);

                    await _notificationService.CreateDeliveryScheduledNotificationAsync(customerId, orderId);

                    _ = Task.Run(async () =>
                    {
                        await _firebaseNotificationService.SendOrderNotificationAsync(
                            customerId.ToString(),
                            NotificationType.DeliveryScheduled,
                            new Dictionary<string, string> { ["orderId"] = orderId }
                        );
                    });
                }


                foreach (var orderId in request.OrderIds)
                {

                    await _notificationService.CreateAssignedDeliveryNotificationAsync(driverId, orderId);

                    _ = Task.Run(async () =>
                    {
                        await _firebaseNotificationService.SendOrderNotificationAsync(
                            driverId.ToString(),
                            NotificationType.AssignedDelivery,
                            new Dictionary<string, string> { ["orderId"] = orderId }
                        );
                    });
                }

                return Ok(new { Message = "Giao việc giao hàng cho tài xế thành công!" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                // Lỗi logic: order chưa sẵn sàng
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                // Lỗi server
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Xóa Order và toàn bộ dữ liệu liên quan (OrderStatusHistory, OrderPhotos, OrderItems, v.v.). 
        /// Kèm xóa file trên Backblaze (nếu có) ở OrderPhotos.
        /// </summary>
        /// <param name="orderId">Mã đơn hàng cần xóa.</param>
        /// <remarks>
        /// **Logic**:
        /// 1) Tìm Order. Nếu không có => 404.
        /// 2) Xóa theo thứ tự: OrderPhoto -> OrderStatusHistory -> OrderExtras -> OrderItems 
        ///    -> OrderAssignmentHistory -> Payments -> DriverLocationHistory -> Ratings -> OrderDiscounts -> Orders
        /// 
        /// **Response codes**:
        /// - 200: Xoá thành công.
        /// - 404: Không tìm thấy orderId.
        /// - 400: Lỗi logic (nếu cần).
        /// - 401: Không có quyền.
        /// - 500: Lỗi server.
        /// </remarks>
        [HttpDelete("orders/{orderId}")]
        public async Task<IActionResult> DeleteOrder(string orderId)
        {
            try
            {
                await _adminService.DeleteOrderAsync(orderId);
                return Ok(new { Message = "Xóa đơn hàng thành công!" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Lỗi: {ex.Message}" });
            }
        }

        /// <summary>
        /// Xóa nhiều Order cùng lúc, bao gồm tất cả dữ liệu liên quan.
        /// </summary>
        /// <param name="request">Chứa danh sách <c>OrderIds</c> cần xóa.</param>
        /// <remarks>
        /// **Logic**:
        /// 1) Lặp qua mỗi OrderId => Thực hiện xóa giống như <c>DeleteOrder</c>.
        /// 2) Dùng Transaction chung, nếu 1 đơn lỗi => rollback tất cả.
        /// 
        /// **Response Codes**:
        /// - `200 OK`: Xóa thành công tất cả.
        /// - `404 Not Found`: Có ít nhất 1 Order không tồn tại => rollback.
        /// - `400 Bad Request`: Danh sách rỗng, logic khác.
        /// - `401 Unauthorized`: Không có quyền Admin.
        /// - `500 Internal Server Error`: Lỗi server.
        /// </remarks>
        [HttpDelete("orders/batch")]
        public async Task<IActionResult> DeleteOrders([FromBody] DeleteOrdersRequest request)
        {
            try
            {
                await _adminService.DeleteOrdersAsync(request.OrderIds);
                return Ok(new { Message = "Xóa nhiều đơn hàng thành công!" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Lỗi: {ex.Message}" });
            }
        }

        /// <summary>
        /// Thêm hoặc cập nhật danh sách khu vực (Area) theo loại AreaType.
        /// </summary>
        /// <param name="request">
        ///     Đối tượng <see cref="AddAreasRequest"/> chứa thông tin cần cập nhật:
        ///     <br/>
        ///     - <c>AreaType</c>: Loại khu vực cần thêm hoặc cập nhật (chỉ nhận "ShippingFee" hoặc "Driver").<br/>
        ///     - <c>Areas</c>: Danh sách các khu vực, mỗi khu vực gồm tên và danh sách quận.
        /// </param>
        /// <returns>
        ///     Trả về kết quả thành công hoặc lỗi trong quá trình xử lý.
        /// </returns>
        [HttpPost("areas")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> AddOrReplaceAreas([FromBody] AddAreasRequest request)
        {
            try
            {
                await _areaService.AddOrReplaceAreasAsync(request);
                return Ok(new { Message = "Cập nhật danh sách Khu vực thành công." });
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
        /// Lấy danh sách Khu vực theo AreaType, sắp xếp theo Name.
        /// </summary>
        /// <param name="areaType">Giá trị AreaType (ShippingFee | Driver ...)</param>
        /// <returns>Danh sách <see cref="AreaItem"/></returns>
        /// <remarks>
        /// **Response codes**  
        /// - 200: Thành công  
        /// - 400: Thiếu / sai areaType  
        /// - 500: Lỗi server
        /// </remarks>
        [HttpGet("areas")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<AreaItemResponse>), 200)]
        public async Task<IActionResult> GetAreas([FromQuery] string areaType)
        {
            try
            {
                var result = await _areaService.GetAreasByTypeAsync(areaType);
                return Ok(result);
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

        [HttpPut("areas/{areaId}")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> UpdateAreaById(Guid areaId, string name, List<string> districts)
        {
            try
            {
                await _areaService.UpdateAreaByIdAsync(areaId, name, districts);
                return Ok(new { Message = "Cập nhật khu vực thành công." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
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

        [HttpDelete("areas/{areaId}")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> DeleteAreaById(Guid areaId)
        {
            try
            {
                await _areaService.DeleteAreaByIdAsync(areaId);
                return Ok(new { Message = "Xóa khu vực thành công." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
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

        [HttpPost("branchaddress")]
        [ProducesResponseType(typeof(Branchaddress), 200)]
        public async Task<IActionResult> AddBranchAddress([FromBody] AddBranchAddressRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { Message = "Invalid input data", Errors = ModelState });
                }

                var result = await _areaService.AddBranchAddressAsync(request);
                return Ok(result);
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

        [HttpGet("branchaddress")]
        [ProducesResponseType(typeof(Branchaddress), 200)]
        public async Task<IActionResult> GetBranchAddress()
        {
            try
            {
                var result = await _areaService.GetBranchAddressAsync();
                return Ok(result);
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

        [HttpPut("branchaddress/{branchId}")]
        [ProducesResponseType(typeof(Branchaddress), 200)]
        public async Task<IActionResult> UpdateBranchAddress(Guid branchId, [FromBody] UpdateBranchAddressRequest request)
        {
            try
            {
                var result = await _areaService.UpdateBranchAddressAsync(branchId, request);
                return Ok(result);
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
