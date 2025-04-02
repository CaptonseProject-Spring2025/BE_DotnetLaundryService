using LaundryService.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [ApiController]
    [Route("api/staff")]
    [Authorize(Roles = "Staff")]
    public class StaffController : BaseApiController
    {
        private readonly IOrderService _orderService;

        public StaffController(IOrderService orderService)
        {
            _orderService = orderService;
        }

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
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        [HttpPost("process-order/{orderId}")]
        public async Task<IActionResult> ProcessOrder(Guid orderId)
        {
            try
            {
                await _orderService.ProcessOrderAsync(HttpContext, orderId);
                return Ok(new { Message = "Đơn hàng đã được nhận xử lý (processing) thành công." });
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
    }
}
