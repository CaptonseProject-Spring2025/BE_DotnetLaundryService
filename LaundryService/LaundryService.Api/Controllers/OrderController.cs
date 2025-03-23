using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace LaundryService.Api.Controllers
{
    /// <summary>
    /// Controller quản lý các thao tác về Order, giỏ hàng (cart)
    /// </summary>
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
        /// Thêm một dịch vụ (ServiceDetail) và các Extras (tùy chọn) vào giỏ hàng (cart)
        /// </summary>
        /// <param name="request">
        ///     - <c>ServiceDetailId</c>: ID của ServiceDetail  
        ///     - <c>Quantity</c>: Số lượng  
        ///     - <c>ExtraIds</c>: Danh sách ID của các Extra (tùy chọn)
        /// </param>
        /// <returns>Trả về thông báo thêm thành công</returns>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (có JWT).  
        /// 
        /// **Logic**:
        /// 1) Lấy userId từ token.  
        /// 2) Tìm ServiceDetail tương ứng <c>request.ServiceDetailId</c>.  
        /// 3) Tìm hoặc tạo Order có trạng thái `"INCART"` cho user này.  
        /// 4) Xử lý các ExtraIds (nếu có). Nếu có ExtraId không tồn tại => `ApplicationException`.  
        /// 5) Kiểm tra xem đã có OrderItem trùng ServiceDetail và EXACT danh sách Extras chưa.  
        ///     - Nếu **có** => tăng Quantity.  
        ///     - Nếu **chưa** => thêm OrderItem mới + OrderExtras mới.  
        /// 
        /// **Response codes**:
        /// - **200**: Thêm thành công
        /// - **400**: ExtraIds không tồn tại hoặc logic sai
        /// - **401**: Chưa đăng nhập, token không hợp lệ
        /// - **404**: ServiceDetail không tìm thấy
        /// - **500**: Lỗi server
        /// </remarks>
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

        /// <summary>
        /// Lấy giỏ hàng (cart) hiện tại của user – Order trạng thái "INCART"
        /// </summary>
        /// <returns>
        ///     Trả về <see cref="CartResponse"/> gồm:
        ///     - <c>OrderId</c>  
        ///     - <c>Items</c>: danh sách các CartItemResponse  
        ///         - Mỗi item có <c>ServiceName</c>, <c>ServicePrice</c>, <c>Quantity</c>, Extras, <c>SubTotal</c>
        ///     - <c>EstimatedTotal</c>: tổng tạm tính của cart
        /// </returns>
        /// <remarks> 
        /// **Yêu cầu**: Đã đăng nhập.  
        /// 
        /// **Logic**:
        /// 1) Lấy userId từ token.  
        /// 2) Tìm Order có `Currentstatus == "INCART"` cho user này.  
        /// 3) Nếu không có => ném `KeyNotFoundException`.  
        /// 4) Tính tổng tạm tính: `(servicePrice + sumExtraPrices) * quantity`, cộng dồn cho các item.  
        /// 
        /// **Response codes**:
        /// - **200**: Trả về giỏ hàng
        /// - **404**: Không tìm thấy cart (user chưa thêm gì)
        /// - **401**: Chưa đăng nhập hoặc token không hợp lệ
        /// - **500**: Lỗi server
        /// </remarks>
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

        /// <summary>
        /// Xác nhận đặt hàng, chuyển Order từ trạng thái "INCART" sang "PENDING".
        /// </summary>
        /// <param name="request">
        ///     <see cref="PlaceOrderRequest"/> gồm:
        ///     - <c>PickupAddressId</c>: Guid địa chỉ lấy đồ (của user)  
        ///     - <c>DeliveryAddressId</c>: Guid địa chỉ trả đồ (của user)  
        ///     - <c>Pickuptime</c>, <c>Deliverytime</c>: Thời gian user mong muốn lấy/trả đồ (tùy chọn)  
        ///     - <c>Shippingfee</c>, <c>Shippingdiscount</c>, <c>Applicablefee</c>, <c>Discount</c>, <c>Total</c>: Các chi phí liên quan, tổng tạm tính  
        ///     - <c>Note</c>: Ghi chú (tùy chọn)  
        ///     - <c>Createdat</c>: Thời gian cập nhật status (tùy chọn, mặc định = UtcNow)
        /// </param>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (có JWT).  
        ///
        /// **Response codes**:
        /// - **200**: Đặt hàng thành công, chuyển trạng thái => "PENDING"
        /// - **400**: Lỗi logic (tổng tiền không khớp, v.v.)
        /// - **401**: Chưa đăng nhập hoặc token không hợp lệ
        /// - **404**: Không tìm thấy order INCART hoặc không tìm thấy địa chỉ
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize]
        [HttpPost("place-order")]
        public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var orderId = await _orderService.PlaceOrderAsync(HttpContext, request);
                return Ok(new { OrderId = orderId, Message = "Đặt hàng thành công! Trạng thái: PENDING" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                // Lỗi logic (tổng tiền không khớp, v.v.)
                return BadRequest(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }
    }
}
