using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Responses;
using LaundryService.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [ApiController]
    [Route("api/staff")]
    [Authorize(Roles = "Staff,Admin")]
    public class StaffController : BaseApiController
    {
        private readonly IStaffService _staffService;

        public StaffController(IStaffService staffService)
        {
            _staffService = staffService;
        }

        /// <summary>
        /// STEP 1: Lấy danh sách các đơn đã PICKEDUP cho staff Checking
        /// </summary>
        /// <returns>Danh sách <see cref="PickedUpOrderResponse"/>.</returns>
        /// <remarks>
        /// **Yêu cầu**: Đăng nhập với vai trò Staff hoặc Admin
        /// 
        /// **Logic**:
        /// 1) Tìm Order.Currentstatus="PICKEDUP"
        /// 2) Phải có ít nhất 1 OrderAssignmentHistory với Status="PICKUP_SUCCESS"
        /// 3) Sort: Emergency = true trước, sau đó theo DeliveryTime gần nhất.
        ///   - Emergency DESC
        ///   - DeliveryTime ASC
        /// </remarks>
        [HttpGet("orders/pickedup")]
        [ProducesResponseType(typeof(List<PickedUpOrderResponse>), 200)]
        public async Task<IActionResult> GetPickedUpOrders()
        {
            try
            {
                var result = await _staffService.GetPickedUpOrdersAsync(HttpContext);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log...
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// STEP 2.1: Staff nhận đơn giặt (đơn đang PICKEDUP) => chuyển trạng thái sang CHECKING.
        /// </summary>
        /// <param name="orderId">ID đơn hàng cần nhận.</param>
        /// <remarks>
        /// **Logic**:
        /// 1) Kiểm tra orderStatus == "PICKEDUP". Nếu không => 400
        /// 2) Cập nhật order => status = "CHECKING"
        /// 3) Tạo orderStatusHistory => Status="CHECKING"
        /// 4) Trả về message success
        /// </remarks>
        /// <response code="200">Đơn hàng nhận giặt thành công.</response>
        /// <response code="400">Đơn không ở trạng thái PICKEDUP.</response>
        /// <response code="404">Không tìm thấy đơn.</response>
        /// <response code="401">Không có quyền.</response>
        /// <response code="500">Lỗi server.</response>
        [HttpPost("orders/receive-for-check")]
        public async Task<IActionResult> ReceiveOrderForCheck([FromQuery] string orderId)
        {
            try
            {
                await _staffService.ReceiveOrderForCheckAsync(HttpContext, orderId);
                return Ok(new { Message = "Đã nhận đơn để kiểm tra/giặt (CHECKING) thành công." });
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
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// STEP 2.2: Lấy các đơn hiện đang CHECKING mà Staff này (từ JWT) đã cập nhật sang CHECKING.
        /// </summary>
        /// <remarks>
        /// **Logic**:
        /// 1) Tìm OrderStatusHistory có Status = "CHECKING", Updatedby = staffId
        /// 2) Lọc các Order cùng Currentstatus = "CHECKING"
        /// 3) Map -> Trả về danh sách
        /// </remarks>
        /// <returns>Danh sách <see cref="CheckingOrderResponse"/></returns>
        /// <response code="200">Trả về danh sách đơn checking của Staff</response>
        /// <response code="401">Không có quyền</response>
        /// <response code="500">Lỗi server</response>
        [HttpGet("orders/checking")]
        public async Task<IActionResult> GetCheckingOrders()
        {
            try
            {
                var orders = await _staffService.GetCheckingOrdersAsync(HttpContext);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                // Log ...
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// STEP 3: Staff cập nhật ảnh + ghi chú cho đơn đang CHECKING (mà staff này phụ trách).
        /// </summary>
        /// <param name="orderId">Mã đơn hàng</param>
        /// <param name="notes">Ghi chú (optional)</param>
        /// <param name="files">Danh sách ảnh (optional)</param>
        /// <remarks>
        /// **Logic**:
        /// 1) Xác thực staff là người đã cập nhật đơn sang "CHECKING"
        /// 2) Nếu có notes => update notes cho record Orderstatushistory.
        /// 3) Nếu có files => upload => ghi vào Orderphoto.
        /// 
        ///  **Request Body**: Phải là `multipart/form-data`.
        /// - `OrderId`: string (bắt buộc)
        /// - `Notes`: string (tùy chọn)
        /// - `Files`: list các file ảnh (tùy chọn)
        /// 
        /// **Response Codes**:
        /// - `200 OK`: Cập nhật thành công.
        /// - `400 Bad Request`: Dữ liệu không hợp lệ, Order không đúng trạng thái.
        /// - `401 Unauthorized`: Staff không có quyền cập nhật đơn này.
        /// - `404 Not Found`: Không tìm thấy Order.
        /// - `500 Internal Server Error`: Lỗi upload file hoặc lỗi hệ thống khác.
        /// </remarks>
        [HttpPost("orders/checking/update")]
        [ProducesResponseType(typeof(CheckingOrderUpdateResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateCheckingOrder(
            [FromForm] string orderId,
            [FromForm] string? notes,
            [FromForm] IFormFileCollection? files
        )
        {
            try
            {
                // Gọi service => ném exception nếu không hợp lệ
                var result = await _staffService.UpdateCheckingOrderAsync(HttpContext, orderId, notes, files);
                return Ok(result); // CheckingOrderUpdateResponse
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
                // Log...
                return StatusCode(500, new { Message = $"An error occurred: {ex.Message}" });
            }
        }

        /// <summary>
        /// STEP 4: Staff xác nhận đơn hàng CHECKING => CHECKED, sau khi kiểm tra xong.
        /// </summary>
        /// <param name="orderId">Mã đơn hàng (bắt buộc)</param>
        /// <param name="notes">Ghi chú (tùy chọn)</param>
        /// <remarks>
        /// **Logic**:
        /// 1) Kiểm tra order có status = CHECKING
        /// 2) Kiểm tra có phải là staff đã cập nhật đơn này sang CHECKING không
        /// 3) Update Order => CHECKED
        /// 4) Thêm Orderstatushistory => status=CHECKED
        /// </remarks>
        /// <response code="200">Xác nhận CHECKED thành công</response>
        /// <response code="400">Order không ở trạng thái CHECKING hoặc staff không phải người xử lý</response>
        /// <response code="404">Không tìm thấy Order</response>
        /// <response code="500">Lỗi server</response>
        [HttpPost("orders/checking/confirm")]
        public async Task<IActionResult> ConfirmCheckingDone([FromQuery] string orderId, [FromQuery] string? notes)
        {
            try
            {
                await _staffService.ConfirmCheckingDoneAsync(HttpContext, orderId, notes ?? "");
                return Ok(new { Message = "Đơn hàng đã được xác nhận CHECKED thành công." });
            }
            catch (KeyNotFoundException ex)
            {
                // 404
                return NotFound(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                // 400
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                // 500
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// STEP 5: Lấy các đơn hàng đang ở trạng thái CHECKED (để nhân viên kế tiếp nhận, mang đi giặt).
        /// </summary>
        /// <returns>Danh sách <see cref="PickedUpOrderResponse"/> mô tả các đơn CHECKED.</returns>
        /// <remarks>
        /// **Logic**:
        /// 1) Tìm Orders có `Currentstatus = "CHECKED"`.
        /// 2) Sắp xếp:
        ///    - `Emergency = true` trước,
        ///    - `Deliverytime` sớm trước.
        ///
        /// **Response codes**:
        /// - `200`: Trả về danh sách.
        /// - `401`: Không có quyền (chưa đăng nhập or sai role).
        /// - `500`: Lỗi server.
        /// </remarks>
        [HttpGet("orders/checked")]
        [ProducesResponseType(typeof(List<PickedUpOrderResponse>), 200)]
        public async Task<IActionResult> GetCheckedOrders()
        {
            try
            {
                var result = await _staffService.GetCheckedOrdersAsync(HttpContext);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log...
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// STEP 6.1: Staff nhận đơn giặt (đơn đang CHECKED) => chuyển trạng thái sang WASHING.
        /// Cho phép đính kèm ghi chú và upload ảnh (tùy chọn).
        /// </summary>
        /// <param name="orderId">Mã đơn hàng (bắt buộc)</param>
        /// <param name="notes">Ghi chú (tùy chọn)</param>
        /// <param name="files">Danh sách file ảnh (tùy chọn)</param>
        /// <returns>Trả về thông báo thành công và Statushistoryid mới</returns>
        /// <remarks>
        /// **Logic**:
        /// 1) Kiểm tra order đang ở trạng thái CHECKED.
        /// 2) Nếu có ảnh => thêm ảnh
        /// 3) Trả về `Statushistoryid` mới.
        ///
        /// **Request**:
        /// - `multipart/form-data`
        /// 
        /// **Response codes**:
        /// - `200 OK`: Thành công, trả về `{ Statushistoryid = ..., Message = ... }`
        /// - `400 Bad Request`: Order không ở trạng thái CHECKED hoặc fail upload file
        /// - `404 Not Found`: Không tìm thấy order
        /// - `401 Unauthorized`: Staff không hợp lệ
        /// - `500 Internal Server Error`: Lỗi server
        /// </remarks>
        [HttpPost("orders/washing/receive")]
        public async Task<IActionResult> ReceiveOrderForWashing(
            [FromForm] string orderId,
            [FromForm] string? notes,
            [FromForm] IFormFileCollection? files
        )
        {
            try
            {
                var statusHistoryId = await _staffService.ReceiveOrderForWashingAsync(HttpContext, orderId, notes, files);
                return Ok(new
                {
                    Statushistoryid = statusHistoryId,
                    Message = "Đơn hàng đã được chuyển sang trạng thái WASHING thành công."
                });
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
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// STEP 6.2: Lấy danh sách các đơn WASHING mà staff hiện tại (theo JWT) đã nhận và cập nhật.
        /// </summary>
        /// <remarks>
        /// 
        /// **Response codes**:
        /// - <c>200</c>: Trả về danh sách
        /// - <c>401</c>: Không có quyền
        /// - <c>500</c>: Lỗi server
        /// </remarks>
        [HttpGet("orders/washing")]
        [ProducesResponseType(typeof(List<PickedUpOrderResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetWashingOrders()
        {
            try
            {
                var orders = await _staffService.GetWashingOrdersAsync(HttpContext);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                // Log...
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// STEP 7: Staff cập nhật ảnh + ghi chú cho đơn đang WASHING (mà staff này phụ trách).
        /// </summary>
        /// <param name="orderId">Mã đơn hàng</param>
        /// <param name="notes">Ghi chú (optional)</param>
        /// <param name="files">Danh sách ảnh (optional)</param>
        /// <remarks>
        /// **Logic**:
        /// 1) Xác thực staff là người đã cập nhật đơn sang "WASHING"
        /// 2) Nếu có notes => update notes cho record Orderstatushistory.
        /// 3) Nếu có files => upload => ghi vào Orderphoto.
        /// 
        ///  **Request Body**: Phải là `multipart/form-data`.
        /// - `OrderId`: string (bắt buộc)
        /// - `Notes`: string (tùy chọn)
        /// - `Files`: list các file ảnh (tùy chọn)
        /// 
        /// **Response Codes**:
        /// - `200 OK`: Cập nhật thành công.
        /// - `400 Bad Request`: Dữ liệu không hợp lệ, Order không đúng trạng thái.
        /// - `401 Unauthorized`: Staff không có quyền cập nhật đơn này.
        /// - `404 Not Found`: Không tìm thấy Order.
        /// - `500 Internal Server Error`: Lỗi upload file hoặc lỗi hệ thống khác.
        /// </remarks>
        [HttpPost("orders/washing/update")]
        [ProducesResponseType(typeof(CheckingOrderUpdateResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateWashingOrder([FromForm] string orderId, [FromForm] string? notes, [FromForm] IFormFileCollection? files)
        {
            try
            {
                var result = await _staffService.UpdateWashingOrderAsync(HttpContext, orderId, notes, files);
                return Ok(result); // WashingOrderUpdateResponse
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
                // Log...
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// STEP 8: Staff xác nhận đơn hàng đã giặt xong (WASHING -> WASHED).
        /// </summary>
        /// <param name="orderId">Mã đơn hàng (bắt buộc)</param>
        /// <param name="notes">Ghi chú (tùy chọn)</param>
        /// <param name="files">Danh sách ảnh (tùy chọn)</param>
        /// <returns>Trả về <see cref="CheckingOrderUpdateResponse"/> chứa thông tin đơn sau cập nhật</returns>
        /// <remarks>
        /// **Logic**:
        /// 1) Kiểm tra Order đang ở trạng thái WASHING.
        /// 2) Kiểm tra staffId (từ JWT) có phải là người đang xử lý đơn WASHING này không.
        /// 
        /// **Request Body**: `multipart/form-data`
        /// 
        /// **Response code**:
        /// - `200 OK`: Cập nhật thành công, trả về `CheckingOrderUpdateResponse`.
        /// - `400 Bad Request`: Order không ở trạng thái WASHING hoặc Staff không phải người xử lý.
        /// - `404 Not Found`: Không tìm thấy Order.
        /// - `401 Unauthorized`: Không có quyền.
        /// - `500 Internal Server Error`: Lỗi server hoặc upload ảnh.
        /// </remarks>
        [HttpPost("orders/washing/confirm")]
        [ProducesResponseType(typeof(CheckingOrderUpdateResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> ConfirmOrderWashed(
            [FromForm] string orderId,
            [FromForm] string? notes,
            [FromForm] IFormFileCollection? files
        )
        {
            try
            {
                var result = await _staffService.ConfirmOrderWashedAsync(HttpContext, orderId, notes, files);
                return Ok(result);
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
                // Log...
                return StatusCode(500, new { Message = ex.Message });
            }
        }
    }
}
