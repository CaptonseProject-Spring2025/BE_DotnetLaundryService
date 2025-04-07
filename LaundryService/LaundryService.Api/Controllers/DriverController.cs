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
        /// Tài xế bắt đầu đi nhận hàng. Chuyển trạng thái từ ASSIGNED_PICKUP → PICKING_UP.
        /// </summary>
        /// <param name="orderId">ID đơn hàng.</param>
        /// <returns>Trả về message thành công.</returns>
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
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Tài xế xác nhận đã nhận hàng thành công. Chuyển trạng thái từ PICKING_UP → PICKED_UP.
        /// </summary>
        /// <param name="orderId">ID đơn hàng.</param>
        /// <param name="notes">Ghi chú nhận hàng.</param>
        [HttpPost("confirm-picked-up")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> ConfirmOrderPickedUp([FromQuery] string orderId, [FromQuery] string notes)
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

        /// <summary>
        /// Tài xế xác nhận đã nhận hàng về nơi nhận. Chuyển trạng thái từ PICKED_UP → RECEIVED.
        /// </summary>
        /// <param name="orderId">ID đơn hàng.</param>
        /// <param name="notes">Ghi chú (tùy chọn).</param>
        [HttpPost("confirm-received")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> ConfirmOrderReceived([FromQuery] string orderId)
        {
            try
            {
                await _orderService.ConfirmOrderReceivedAsync(HttpContext, orderId);
                return Ok(new { Message = "Tài xế đã nhận hàng về thành công (RECEIVED)." });
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
        /// Tài xế bắt đầu giao hàng. Chuyển trạng thái từ ASSIGNED_DELIVERY → DELIVERING.
        /// </summary>
        /// <param name="orderId">ID đơn hàng.</param>
        [HttpPost("start-delivery")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> StartOrderDelivery([FromQuery] string orderId)
        {
            try
            {
                await _orderService.StartOrderDeliveryAsync(HttpContext, orderId);
                return Ok(new { Message = "Tài xế đã bắt đầu giao hàng (DELIVERING)." });
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
        /// Tài xế xác nhận đã giao hàng thành công. Chuyển từ DELIVERING → DELIVERED.
        /// </summary>
        /// <param name="orderId">ID đơn hàng.</param>
        /// <param name="notes">Ghi chú giao hàng.</param>
        [HttpPost("confirm-delivered")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> ConfirmOrderDelivered([FromQuery] string orderId, [FromQuery] string notes)
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

    }
}
