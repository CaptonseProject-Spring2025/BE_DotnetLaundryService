using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Pagination;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [ApiController]
    [Route("api/customer-staff")]
    [Authorize(Roles = "CustomerStaff")]
    public class CustomerStaffController : BaseApiController
    {
        private readonly IOrderService _orderService;

        public CustomerStaffController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        /// <summary>
        /// Lấy danh sách các đơn hàng đang chờ xử lý (status = "PENDING") dành cho CustomerStaff.
        /// </summary>
        /// <param name="page">Trang hiện tại (mặc định = 1).</param>
        /// <param name="pageSize">Số lượng bản ghi mỗi trang (mặc định = 10).</param>
        /// <remarks>
        /// **Mục đích**: Cho phép nhân viên lấy danh sách các đơn hàng chờ xử lý.
        ///
        /// **Logic xử lý**:
        /// 1. Chỉ lấy các đơn có trạng thái `"PENDING"`
        /// 2. Bỏ qua các đơn đang được CustomerStaff khác xử lý
        ///
        /// **Yêu cầu**:
        /// - Đã đăng nhập với vai trò `CustomerStaff`
        ///
        /// **Response codes**:
        /// - <c>200</c>: Thành công
        /// - <c>500</c>: Lỗi hệ thống
        /// </remarks>
        [HttpGet("pending-orders")]
        public async Task<IActionResult> GetPendingOrders(int page = 1, int pageSize = 10)
        {
            try
            {
                // Gọi service
                var result = await _orderService.GetPendingOrdersForStaffAsync(HttpContext, page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // log ex
                return StatusCode(500, new { Message = $"An unexpected error occurred: {ex.Message}" });
            }
        }

        /// <summary>
        /// Nhận xử lý một đơn hàng đang chờ (PENDING). Chỉ một nhân viên có thể xử lý tại một thời điểm.
        /// </summary>
        /// <param name="orderId">ID đơn hàng cần nhận xử lý.</param>
        /// <returns>
        /// Trả về message xác nhận đơn đã được nhận xử lý.
        /// </returns>
        /// <remarks>
        /// **Mục đích**: Gán đơn hàng cho nhân viên để bắt đầu xử lý.
        ///
        /// **Logic xử lý**:
        /// 1. Kiểm tra đơn hàng có tồn tại và đang ở trạng thái `"PENDING"`
        /// 2. Kiểm tra đơn đã được nhân viên khác nhận chưa
        /// 3. Nếu chưa có, tạo mới 1 bản ghi `OrderAssignmentHistory` với:
        ///     - `AssignedTo = current staff`
        ///     - `Status = "PROCESSING"`
        ///
        /// **Yêu cầu**:
        /// - Đã đăng nhập với vai trò `CustomerStaff`
        ///
        /// **Response codes**:
        /// - <c>200</c>: Nhận xử lý thành công
        /// - <c>400</c>: Đơn không ở trạng thái PENDING hoặc đang bị người khác nhận
        /// - <c>404</c>: Không tìm thấy đơn hàng
        /// - <c>401</c>: Không có quyền
        /// - <c>500</c>: Lỗi hệ thống
        /// </remarks>
        [HttpPost("process-order/{orderId}")]
        public async Task<IActionResult> ProcessOrder(Guid orderId)
        {
            try
            {
                // Gọi service -> nhận về assignmentId
                var assignmentId = await _orderService.ProcessOrderAsync(HttpContext, orderId);

                return Ok(new
                {
                    Message = "Đơn hàng đã được nhận xử lý (processing) thành công.",
                    AssignmentId = assignmentId
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                // Lỗi logic (Order not PENDING hoặc có staff khác)
                return BadRequest(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"An unexpected error occurred: {ex.Message}" });
            }
        }

        /// <summary>
        /// Xác nhận hoàn tất xử lý đơn hàng. Chuyển trạng thái đơn từ "PENDING" sang "CONFIRMED".
        /// </summary>
        /// <param name="orderId">ID đơn hàng.</param>
        /// <param name="notes">Ghi chú xử lý (tùy chọn).</param>
        /// <returns>Trả về message xác nhận thành công.</returns>
        /// <remarks>
        /// **Mục đích**: Khi nhân viên hoàn tất xử lý đơn, họ xác nhận để chuyển trạng thái đơn hàng.
        ///
        /// **Logic xử lý**:
        /// 1. Kiểm tra có bản ghi `OrderAssignmentHistory` với `Status = "PROCESSING"`
        ///     - Nếu không có => lỗi
        /// 2. Cập nhật bản ghi này:  
        ///     - `Status = "SUCCESS"`
        /// 3. Cập nhật đơn hàng:  
        ///     - `CurrentStatus = "CONFIRMED"`
        /// 4. Thêm một dòng vào `OrderStatusHistory` với `Status = "CONFIRMED"` và ghi chú (nếu có)
        ///
        /// **Yêu cầu**:
        /// - Đăng nhập với role `CustomerStaff`
        ///
        /// **Response codes**:
        /// - <c>200</c>: Xác nhận thành công
        /// - <c>400</c>: Không tìm thấy processing assignment hoặc lỗi logic
        /// - <c>404</c>: Không tìm thấy đơn hàng
        /// - <c>401</c>: Không có quyền
        /// - <c>500</c>: Lỗi hệ thống
        /// </remarks>
        [HttpPost("confirm-order")]
        public async Task<IActionResult> ConfirmOrder([FromQuery] Guid orderId, [FromQuery] string? notes)
        {
            try
            {
                // Gọi service
                await _orderService.ConfirmOrderAsync(HttpContext, orderId, notes ?? "");
                return Ok(new { Message = "Đơn hàng đã được xác nhận (CONFIRMED) thành công." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                // Lỗi logic
                return BadRequest(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"An unexpected error occurred: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy danh sách các đơn hàng đang trong giỏ (trạng thái "INCART") – dành cho CustomerStaff.
        /// </summary>
        /// <param name="page">Trang hiện tại (bắt đầu từ 1, mặc định = 1).</param>
        /// <param name="pageSize">Số bản ghi mỗi trang (mặc định = 10).</param>
        /// <remarks>
        /// **Yêu cầu**:  
        /// - Đã đăng nhập bằng JWT  
        /// - Có role là <c>CustomerStaff</c>  
        ///
        /// **Mục đích**:  
        /// Hỗ trợ quản trị viên theo dõi những đơn chưa được đặt (người dùng chỉ thêm vào giỏ nhưng chưa xác nhận đặt).
        ///
        /// **Logic xử lý**:
        /// 1) Truy vấn tất cả `Order` có `CurrentStatus == "INCART"`.
        /// 2) Trả về danh sách phân trang với thông tin:
        ///     - Người tạo đơn (UserId, FullName, PhoneNumber)
        ///     - Danh sách các món trong giỏ (dịch vụ, extras, số lượng, giá tạm tính)
        ///
        /// **Response codes**:
        /// - <c>200</c>: Lấy danh sách thành công.
        /// - <c>401</c>: Không có quyền truy cập.
        /// - <c>500</c>: Lỗi hệ thống.
        /// </remarks>
        [HttpGet("all-cart")]
        [ProducesResponseType(typeof(PaginationResult<InCartOrderAdminResponse>), 200)]
        public async Task<IActionResult> GetInCartPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _orderService.GetInCartOrdersPagedAsync(HttpContext, page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"An unexpected error occurred: {ex.Message}" });
            }
        }

        [HttpPost("cancel-order")]
        public async Task<IActionResult> CancelOrder([FromQuery] Guid assignmentId, [FromQuery] string notes)
        {
            // Chú ý: assignmentId & notes là bắt buộc -> check or model validation
            if (assignmentId == Guid.Empty || string.IsNullOrWhiteSpace(notes))
            {
                return BadRequest(new { Message = "assignmentId & notes đều bắt buộc." });
            }

            try
            {
                await _orderService.CancelOrderAsync(HttpContext, assignmentId, notes);
                return Ok(new { Message = "Đơn hàng đã được hủy thành công." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"An unexpected error occurred: {ex.Message}" });
            }
        }

    }
}
