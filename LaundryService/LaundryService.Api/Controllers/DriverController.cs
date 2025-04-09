using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [ApiController]
    [Route("api/driver")]
    [Authorize(Roles = "Driver")]
    public class DriverController : BaseApiController
    {
        private readonly IOrderService _orderService;
        private readonly IAddressService _addressService;
        private readonly IOrderAssignmentHistoryService _orderAssignmentHistoryService;

        public DriverController(IOrderService orderService, IAddressService addressService, IOrderAssignmentHistoryService orderAssignmentHistoryService)
        {
            _orderService = orderService;
            _addressService = addressService;
            _orderAssignmentHistoryService = orderAssignmentHistoryService;
        }

        /// <summary>
        /// Đánh dấu rằng tài xế đã bắt đầu đi nhận đơn hàng (trạng thái <c>PICKINGUP</c>)
        /// </summary>
        /// <param name="orderId">
        /// ID của đơn hàng cần nhận hàng
        /// </param>
        /// <returns>
        /// Trả về thông báo tài xế đã bắt đầu nhận hàng
        /// </returns>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (có JWT) và là tài xế được chỉ định thực hiện đơn nhận hàng này.  
        /// 
        /// **Logic**:
        /// 1) Lấy <c>userId</c> từ token.  
        /// 2) Kiểm tra đơn hàng có tồn tại không.  
        /// 3) Kiểm tra đơn hàng phải đang ở trạng thái <c>SCHEDULED_PICKUP</c>.  
        /// 4) Kiểm tra tài xế có assignment phù hợp (status = <c>ASSIGNED_PICKUP</c>, đúng user).  
        /// 5) Kiểm tra tài xế chưa có đơn nào khác đang ở trạng thái <c>PICKINGUP</c>.  
        /// 6) Cập nhật trạng thái đơn hàng sang <c>PICKINGUP</c>.  
        /// 7) Ghi lại lịch sử trạng thái trong <c>Orderstatushistory</c>.  
        /// 
        /// **Response codes**:
        /// - **200**: Cập nhật trạng thái thành công  
        /// - **400**: Logic sai, ví dụ như đơn đang ở trạng thái không hợp lệ  
        /// - **401**: Chưa đăng nhập hoặc không có quyền nhận đơn này  
        /// - **404**: Đơn hàng không tồn tại  
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize]
        [HttpPost("start-pickup")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> StartOrderPickup([FromQuery] string orderId)
        {
            try
            {
                await _orderService.StartOrderPickupAsync(HttpContext, orderId);
                return Ok(new { Message = "Tài xế đã bắt đầu đi nhận hàng (PICKING_UP)." });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                // Log ra logger nào đó
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Tài xế xác nhận đã nhận hàng thành công (trạng thái <c>PICKED_UP</c>), kèm ảnh và ghi chú.
        /// </summary>
        /// <param name="orderId">ID của đơn hàng cần xác nhận đã nhận</param>
        /// <param name="notes">Ghi chú (nếu có) khi nhận hàng</param>
        /// <param name="files">Danh sách ảnh (ít nhất một) chụp khi nhận hàng</param>
        /// <returns>
        /// Trả về thông báo xác nhận thành công
        /// </returns>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (có JWT), là tài xế được phân công nhận đơn, đơn hàng đang ở trạng thái <c>PICKINGUP</c>.  
        /// 
        /// **Logic**:
        /// 1) Lấy <c>userId</c> từ token.  
        /// 2) Kiểm tra đơn hàng tồn tại và đang ở trạng thái <c>PICKINGUP</c>.  
        /// 3) Kiểm tra tài xế là người được phân công nhận đơn này với status assignment <c>ASSIGNED_PICKUP</c>.  
        /// 4) Kiểm tra có ít nhất một ảnh đính kèm. Nếu không có => lỗi.  
        /// 5) Cập nhật trạng thái đơn hàng sang <c>PICKED_UP</c>.  
        /// 6) Ghi nhận lịch sử trạng thái kèm <c>notes</c>.  
        /// 7) Upload ảnh lên hệ thống, lưu link vào bảng <c>Orderphoto</c> gắn với lịch sử trạng thái.  
        /// 
        /// **Response codes**:
        /// - **200**: Xác nhận thành công  
        /// - **400**: Không có ảnh hoặc logic sai  
        /// - **401**: Chưa đăng nhập hoặc không được giao đơn này  
        /// - **404**: Đơn hàng không tồn tại  
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpPost("confirm-picked-up")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> ConfirmOrderPickedUp(
        [FromForm] string orderId,
        [FromForm] string notes,
        [FromForm] List<IFormFile> files)
        {
            try
            {
                await _orderService.ConfirmOrderPickedUpAsync(HttpContext, orderId, notes);
                return Ok(new { Message = "Tài xế đã xác nhận nhận hàng thành công (PICKED_UP)." });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
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


        //[HttpPost("confirm-picked-up")]
        //[ProducesResponseType(typeof(string), 200)]
        //public async Task<IActionResult> ConfirmOrderPickedUp([FromQuery] string orderId, [FromQuery] string notes)
        //{
        //    try
        //    {
        //        await _orderService.ConfirmOrderPickedUpAsync(HttpContext, orderId, notes);
        //        return Ok(new { Message = "Tài xế đã xác nhận nhận hàng thành công (PICKED_UP)." });
        //    }
        //    catch (ApplicationException ex)
        //    {
        //        return BadRequest(new { Message = ex.Message });
        //    }
        //    catch (KeyNotFoundException ex)
        //    {
        //        return NotFound(new { Message = ex.Message });
        //    }
        //    catch (UnauthorizedAccessException ex)
        //    {
        //        return Unauthorized(new { Message = ex.Message });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
        //    }
        //}

        /// <summary>
        /// Xác nhận rằng tài xế đã mang hàng về thành công (kết thúc quá trình nhận hàng).
        /// </summary>
        /// <param name="orderId">ID của đơn hàng đã nhận về thành công</param>
        /// <returns>Trả về thông báo xác nhận hoàn tất nhận hàng</returns>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (JWT), là tài xế được phân công nhận đơn, đơn hàng đang ở trạng thái <c>PICKEDUP</c>.  
        /// 
        /// **Logic**:
        /// 1) Lấy <c>userId</c> từ token.  
        /// 2) Kiểm tra đơn hàng tồn tại và đang ở trạng thái <c>PICKEDUP</c>.  
        /// 3) Kiểm tra tài xế là người được phân công với assignment status là <c>ASSIGNED_PICKUP</c>.  
        /// 4) Cập nhật assignment sang trạng thái <c>PICKUP_SUCCESS</c> và gán <c>CompletedAt</c>.  
        /// 5) Ghi lại lịch sử trạng thái trong <c>Orderstatushistory</c>.  
        /// 
        /// **Response codes**:
        /// - **200**: Xác nhận thành công  
        /// - **400**: Logic sai (ví dụ trạng thái không đúng)  
        /// - **401**: Không có quyền hoặc chưa đăng nhập  
        /// - **404**: Đơn hàng không tồn tại  
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpPost("confirm-pickup-success")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> ConfirmOrderPickupSuccess([FromQuery] string orderId)
        {
            try
            {
                await _orderService.ConfirmOrderPickupSuccessAsync(HttpContext, orderId);
                return Ok(new { Message = "Tài xế đã nhận hàng về thành công." });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
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
        /// Tài xế huỷ việc nhận đơn hàng đã được phân công (trạng thái <c>ASSIGNED_PICKUP</c>), kèm lý do và ảnh minh chứng.
        /// </summary>
        /// <param name="orderId">ID của đơn hàng muốn huỷ nhận</param>
        /// <param name="reason">Lý do huỷ đơn</param>
        /// <param name="files">Danh sách ảnh (ít nhất một) minh chứng lý do huỷ</param>
        /// <returns>Trả về thông báo huỷ đơn thành công</returns>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (có JWT), là tài xế được phân công, đơn chưa được nhận thành công (chưa ở trạng thái <c>PICKEDUP</c>).  
        /// 
        /// **Logic**:
        /// 1) Kiểm tra <c>cancelReason</c> không được để trống.  
        /// 2) Lấy <c>userId</c> từ token.  
        /// 3) Kiểm tra đơn hàng có tồn tại không.  
        /// 4) Kiểm tra assignment tồn tại, đúng tài xế, đúng trạng thái <c>ASSIGNED_PICKUP</c>.  
        /// 5) Kiểm tra đơn chưa được nhận thành công (chưa có trạng thái <c>PICKEDUP</c>).  
        /// 6) Kiểm tra có ít nhất một ảnh được gửi kèm.  
        /// 7) Cập nhật trạng thái assignment → <c>PICKUP_FAILED</c>, gán <c>CompletedAt</c>.  
        /// 8) Cập nhật trạng thái đơn hàng → <c>PICKUPFAILED</c>.  
        /// 9) Ghi nhận lịch sử trạng thái, bao gồm lý do huỷ.  
        /// 10) Upload ảnh và lưu lại liên kết với lịch sử trạng thái.
        /// 
        /// **Response codes**:
        /// - **200**: Huỷ đơn thành công  
        /// - **400**: Lý do hoặc ảnh không hợp lệ  
        /// - **401**: Không có quyền huỷ hoặc chưa đăng nhập  
        /// - **404**: Đơn hàng không tồn tại  
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpPost("cancel-pickup")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> CancelPickup(
        [FromForm] string orderId,
        [FromForm] string reason,
        [FromForm] List<IFormFile> files)
        {
            try
            {
                await _orderService.CancelAssignedPickupAsync(HttpContext, orderId, reason);
                return Ok(new { Message = "Đã huỷ nhận đơn hàng thành công." });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
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
        /// Tài xế bắt đầu quá trình giao hàng (chuyển đơn hàng sang trạng thái <c>DELIVERING</c>).
        /// </summary>
        /// <param name="orderId">ID của đơn hàng được giao</param>
        /// <returns>Trả về thông báo tài xế bắt đầu giao hàng</returns>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (JWT), là tài xế được phân công giao đơn, đơn hàng đang ở trạng thái <c>SCHEDULED_DELIVERY</c>.  
        /// 
        /// **Logic**:
        /// 1) Lấy <c>userId</c> từ token.  
        /// 2) Kiểm tra đơn hàng tồn tại.  
        /// 3) Kiểm tra trạng thái đơn hàng là <c>SCHEDULED_DELIVERY</c>.  
        /// 4) Kiểm tra tài xế được phân công giao đơn với status <c>ASSIGNED_DELIVERY</c>.  
        /// 5) Kiểm tra không có đơn giao nào khác đang ở trạng thái <c>DELIVERING</c>.  
        /// 6) Cập nhật trạng thái đơn hàng sang <c>DELIVERING</c>.  
        /// 7) Ghi nhận lịch sử trạng thái trong <c>Orderstatushistory</c>.
        /// 
        /// **Response codes**:
        /// - **200**: Cập nhật trạng thái thành công  
        /// - **400**: Logic sai, trạng thái không hợp lệ  
        /// - **401**: Chưa đăng nhập hoặc không có quyền thực hiện  
        /// - **404**: Đơn hàng không tồn tại  
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpPost("start-delivery")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> StartOrderDelivery([FromQuery] string orderId)
        {
            try
            {
                await _orderService.StartOrderDeliveryAsync(HttpContext, orderId);
                return Ok(new { Message = "Tài xế đã bắt đầu đi giao hàng (DELIVERING)." });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
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
        /// Tài xế xác nhận đã giao hàng thành công (chuyển đơn sang trạng thái <c>DELIVERED</c>), kèm ghi chú và ảnh minh chứng.
        /// </summary>
        /// <param name="orderId">ID của đơn hàng đã giao thành công</param>
        /// <param name="notes">Ghi chú (nếu có) khi giao hàng</param>
        /// <param name="files">Danh sách ảnh xác nhận giao hàng (bắt buộc ít nhất 1 ảnh)</param>
        /// <returns>Trả về thông báo xác nhận giao hàng thành công</returns>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (JWT), là tài xế được phân công, đơn hàng đang ở trạng thái <c>DELIVERING</c>.  
        /// 
        /// **Logic**:
        /// 1) Lấy <c>userId</c> từ token.  
        /// 2) Kiểm tra đơn hàng tồn tại.  
        /// 3) Kiểm tra trạng thái đơn là <c>DELIVERING</c>.  
        /// 4) Kiểm tra assignment hợp lệ: đúng đơn, đúng tài xế, đúng trạng thái <c>ASSIGNED_DELIVERY</c>.  
        /// 5) Kiểm tra có ít nhất một ảnh được gửi lên.  
        /// 6) Cập nhật trạng thái đơn hàng → <c>DELIVERED</c>.  
        /// 7) Ghi nhận lịch sử trạng thái, bao gồm <c>notes</c>.  
        /// 8) Upload ảnh xác nhận và lưu liên kết vào <c>Orderphoto</c>.
        /// 
        /// **Response codes**:
        /// - **200**: Xác nhận thành công  
        /// - **400**: Không có ảnh hoặc logic sai  
        /// - **401**: Không có quyền hoặc chưa đăng nhập  
        /// - **404**: Đơn hàng không tồn tại  
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpPost("confirm-delivered")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> ConfirmOrderDelivered(
            [FromForm] string orderId,
            [FromForm] string notes,
            [FromForm] List<IFormFile> files)
        {
            try
            {
                await _orderService.ConfirmOrderDeliveredAsync(HttpContext, orderId, notes);
                return Ok(new { Message = "Tài xế đã xác nhận giao hàng thành công (DELIVERED)." });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
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
        /// Tài xế xác nhận đã hoàn thành đơn giao hàng và đã quay về (chuyển assignment sang trạng thái <c>DELIVERY_SUCCESS</c>).
        /// </summary>
        /// <param name="orderId">ID của đơn hàng đã giao xong</param>
        /// <returns>Trả về thông báo hoàn tất quá trình giao hàng</returns>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (JWT), là tài xế được phân công giao đơn, đơn hàng đã ở trạng thái <c>DELIVERED</c>.  
        /// 
        /// **Logic**:
        /// 1) Lấy <c>userId</c> từ token.  
        /// 2) Kiểm tra đơn hàng có tồn tại và đang ở trạng thái <c>DELIVERED</c>.  
        /// 3) Kiểm tra assignment phù hợp: đúng đơn, đúng tài xế, đúng trạng thái <c>ASSIGNED_DELIVERY</c>.  
        /// 4) Cập nhật trạng thái assignment → <c>DELIVERY_SUCCESS</c>, gán <c>CompletedAt</c>.  
        /// 5) Ghi nhận lịch sử trạng thái với mô tả hoàn tất giao hàng.
        /// 
        /// **Response codes**:
        /// - **200**: Xác nhận thành công  
        /// - **400**: Logic sai, trạng thái không hợp lệ  
        /// - **401**: Không có quyền hoặc chưa đăng nhập  
        /// - **404**: Đơn hàng không tồn tại  
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpPost("confirm-delivery-success")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> ConfirmFinishDelivery([FromQuery] string orderId)
        {
            try
            {
                await _orderService.ConfirmOrderDeliverySuccessAsync(HttpContext, orderId);
                return Ok(new { Message = "Tài xế đã xác nhận giao hàng thành công và đã về." });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
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
        /// Tài xế huỷ việc giao đơn hàng đã được phân công (trạng thái <c>ASSIGNED_DELIVERY</c>), kèm lý do và ảnh minh chứng.
        /// </summary>
        /// <param name="orderId">ID của đơn hàng muốn huỷ giao</param>
        /// <param name="reason">Lý do huỷ đơn giao hàng</param>
        /// <param name="files">Danh sách ảnh (ít nhất một) chứng minh lý do huỷ</param>
        /// <returns>Trả về thông báo huỷ giao đơn thành công</returns>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (JWT), là tài xế được phân công, đơn chưa được giao thành công (chưa ở trạng thái <c>DELIVERED</c>).  
        /// 
        /// **Logic**:
        /// 1) Kiểm tra <c>reason</c> không được để trống.  
        /// 2) Lấy <c>userId</c> từ token.  
        /// 3) Kiểm tra đơn hàng tồn tại.  
        /// 4) Kiểm tra assignment hợp lệ: đúng đơn, đúng tài xế, đúng trạng thái <c>ASSIGNED_DELIVERY</c>.  
        /// 5) Kiểm tra tài xế chưa xác nhận đã giao hàng (<c>DELIVERED</c>).  
        /// 6) Kiểm tra có ít nhất một ảnh đính kèm.  
        /// 7) Cập nhật trạng thái assignment → <c>DELIVERY_FAILED</c>, gán <c>CompletedAt</c>.  
        /// 8) Cập nhật trạng thái đơn hàng → <c>DELIVERYFAILED</c>.  
        /// 9) Ghi lại lịch sử trạng thái với <c>cancelReason</c>.  
        /// 10) Upload ảnh và lưu vào <c>Orderphoto</c> gắn với lịch sử.
        /// 
        /// **Response codes**:
        /// - **200**: Huỷ giao đơn thành công  
        /// - **400**: Lý do hoặc ảnh không hợp lệ  
        /// - **401**: Không có quyền huỷ hoặc chưa đăng nhập  
        /// - **404**: Đơn hàng không tồn tại  
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpPost("cancel-delivery")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> CancelDelivery(
            [FromForm] string orderId,
            [FromForm] string reason,
            [FromForm] List<IFormFile> files)
        {
            try
            {
                await _orderService.CancelAssignedDeliveryAsync(HttpContext, orderId, reason);
                return Ok(new { Message = "Đã huỷ giao đơn hàng thành công." });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
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
        /// Lấy địa chỉ nơi nhận hàng (pickup address) từ assignment được phân.
        /// Chỉ tài xế được phân công mới được truy cập.
        /// </summary>
        /// <param name="assignmentId">ID phân công (assignment).</param>
        /// <returns>Thông tin địa chỉ nơi nhận hàng.</returns>
        [HttpGet("pickup-address")]
        [ProducesResponseType(typeof(AddressInfoResponse), 200)]
        public async Task<IActionResult> GetPickupAddress([FromQuery] Guid assignmentId)
        {
            try
            {
                var result = await _addressService.GetPickupAddressFromAssignmentAsync(HttpContext, assignmentId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
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
        /// Lấy địa chỉ nơi giao hàng (delivery address) từ assignment được phân.
        /// Chỉ tài xế được phân công mới được truy cập.
        /// </summary>
        /// <param name="assignmentId">ID phân công (assignment).</param>
        /// <returns>Thông tin địa chỉ nơi giao hàng.</returns>
        [HttpGet("delivery-address")]
        [ProducesResponseType(typeof(AddressInfoResponse), 200)]
        public async Task<IActionResult> GetDeliveryAddress([FromQuery] Guid assignmentId)
        {
            try
            {
                var result = await _addressService.GetDeliveryAddressFromAssignmentAsync(HttpContext, assignmentId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
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
        /// Lấy danh sách các nhiệm vụ tài xế được phân công.
        /// </summary>
        [HttpGet("my-assignments")]
        [ProducesResponseType(typeof(List<AssignmentHistoryResponse>), 200)]
        public async Task<IActionResult> GetMyAssignments()
        {
            try
            {
                var result = await _orderAssignmentHistoryService.GetAssignmentsForDriverAsync(HttpContext);
                return Ok(result);
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
        /// Lấy chi tiết nhiệm vụ được giao theo AssignmentId.
        /// </summary>
        /// <param name="assignmentId">ID phân công</param>
        /// <returns>Thông tin chi tiết assignment</returns>
        [HttpGet("assignments/{assignmentId}")]
        [ProducesResponseType(typeof(AssignmentDetailResponse), 200)]
        public async Task<IActionResult> GetAssignmentDetail(Guid assignmentId)
        {
            try
            {
                var result = await _orderAssignmentHistoryService.GetAssignmentDetailAsync(HttpContext, assignmentId);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return NotFound(new { Message = ex.Message });
            }
        }
    }
}
