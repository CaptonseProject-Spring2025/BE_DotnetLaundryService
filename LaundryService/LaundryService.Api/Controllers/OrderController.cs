using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrderController : BaseApiController
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        /// <summary>
        /// Endpoint AddToCart
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("add-to-cart")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _orderService.AddToCartAsync(HttpContext, request);
                return Ok(new { Message = "Add to cart successfully." });
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
                // Log ra logger nào đó
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("cart")]
        public async Task<IActionResult> GetCart()
        {
            try
            {
                var result = await _orderService.GetCartAsync(HttpContext);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                // Không tìm thấy cart => 404
                return NotFound(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                // UserId không hợp lệ => 401
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception)
            {
                // Lỗi khác => 500
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }
    }
}
