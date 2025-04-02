using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Pagination;
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
        private readonly IFirebaseNotificationService _firebaseNotificationService;
        private readonly INotificationService _notificationService;
        private readonly IUtil _util;

        public OrderController(
            IOrderService orderService,
            IFirebaseNotificationService firebaseNotificationService,
            INotificationService notificationService,
            IUtil util)
        {
            _orderService = orderService;
            _firebaseNotificationService = firebaseNotificationService;
            _notificationService = notificationService;
            _util = util;
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
                var userId = _util.GetCurrentUserIdOrThrow(HttpContext);
                var orderId = await _orderService.PlaceOrderAsync(HttpContext, request);

                try
                {
                    await _notificationService.CreateOrderPlacedNotificationAsync(userId, orderId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi tạo notification trong hệ thống: {ex.Message}");
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _firebaseNotificationService.SendOrderNotificationAsync(userId.ToString(), NotificationType.OrderPlaced);
                    }
                    catch (Exception ex)
                    {
                        // Log lỗi nhưng không làm ảnh hưởng đến API response
                        Console.WriteLine($"Lỗi gửi thông báo: {ex.Message}");
                    }
                });


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

        /// <summary>
        /// Lấy danh sách các đơn hàng đã đặt của người dùng hiện tại.
        /// (Chỉ lấy các đơn có trạng thái khác "INCART")
        /// </summary>
        /// <returns>
        /// Danh sách <see cref="UserOrderResponse"/> gồm:
        /// - <c>OrderId</c>: Mã đơn hàng  
        /// - <c>OrderName</c>: Tên ngắn gọn mô tả đơn hàng (từ các category dịch vụ)  
        /// - <c>ServiceCount</c>: Tổng số dịch vụ trong đơn  
        /// - <c>TotalPrice</c>: Tổng tiền  
        /// - <c>OrderedDate</c>: Ngày đặt hàng (theo giờ Việt Nam)  
        /// - <c>OrderStatus</c>: Trạng thái đơn hàng hiện tại
        /// </returns>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (có JWT).  
        ///
        /// **Logic xử lý**:
        /// 1) Lấy `userId` từ token.
        /// 2) Truy vấn danh sách đơn hàng của user có `Currentstatus` khác `"INCART"`.
        /// 3) Trả kết quả theo thứ tự mới nhất (`CreatedAt desc`).
        ///
        /// **Response codes**:
        /// - <c>200</c>: Trả về danh sách đơn thành công.
        /// - <c>401</c>: Token không hợp lệ.
        /// - <c>500</c>: Lỗi hệ thống.
        /// </remarks>
        [Authorize]
        [HttpGet("user-orders")]
        //[ProducesResponseType(typeof(List<UserOrderResponse>), 200)]
        public async Task<IActionResult> GetUserOrders()
        {
            try
            {
                var orders = await _orderService.GetUserOrdersAsync(HttpContext);
                return Ok(orders);
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

        /// <summary>
        /// Lấy danh sách tất cả đơn hàng trong hệ thống (phân trang).
        /// Dành cho Admin hoặc Staff.
        /// </summary>
        /// <param name="status">Lọc theo trạng thái đơn hàng (PENDING, CONFIRMED, DONE...). Nếu bỏ trống thì lấy tất cả.</param>
        /// <param name="page">Trang hiện tại (bắt đầu từ 1).</param>
        /// <param name="pageSize">Số lượng bản ghi mỗi trang.</param>
        /// <returns>
        /// Kết quả phân trang <see cref="PaginationResult{UserOrderResponse}"/> gồm:
        /// - <c>Data</c>: Danh sách đơn hàng  
        /// - <c>TotalRecords</c>: Tổng số đơn hàng  
        /// - <c>CurrentPage</c>, <c>PageSize</c>
        /// </returns>
        /// <remarks>
        /// **Yêu cầu**: JWT token hợp lệ, role: `Admin` hoặc `Staff`.  
        ///
        /// **Logic xử lý**:
        /// 1) Lấy danh sách đơn hàng có `Currentstatus` khác `"INCART"`.
        /// 2) Nếu `status` được truyền vào => lọc theo trạng thái.
        /// 3) Trả danh sách theo thứ tự mới nhất (`CreatedAt desc`).
        ///
        /// **Response codes**:
        /// - <c>200</c>: Lấy danh sách thành công.
        /// - <c>401</c>: Token không hợp lệ hoặc không có quyền.
        /// - <c>500</c>: Lỗi hệ thống.
        /// </remarks>
        [Authorize(Roles = "Admin,Staff")]
        [HttpGet("all-orders")]
        public async Task<IActionResult> GetAllOrders([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _orderService.GetAllOrdersAsync(HttpContext, status, page, pageSize);
                return Ok(result);
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

        /// <summary>
        /// Lấy chi tiết một đơn hàng theo <c>orderId</c> (bao gồm địa chỉ, dịch vụ, extras, trạng thái...).
        /// </summary>
        /// <param name="orderId">ID của đơn hàng cần lấy chi tiết.</param>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (JWT).  
        /// **Logic xử lý**:
        /// 
        /// 1) Tìm đơn hàng theo `orderId`, bao gồm:
        ///     - OrderItems → Service  
        ///     - OrderExtras → Extra  
        ///     - OrderStatusHistories
        /// 
        /// 2) Nếu đơn không tồn tại hoặc đang là `"INCART"` thì trả về lỗi 404.  
        /// 3) Nếu người dùng không phải chủ đơn thì trả về lỗi 401.  
        /// 4) Trích xuất thông tin Pickup/Delivery, dịch vụ, extras, trạng thái đơn hàng.
        ///
        /// **Response codes**:
        /// - <c>200</c>: Trả về chi tiết đơn thành công.
        /// - <c>401</c>: Token không hợp lệ hoặc không có quyền xem đơn.
        /// - <c>404</c>: Không tìm thấy đơn hàng.
        /// - <c>500</c>: Lỗi hệ thống.
        /// </remarks>
        [Authorize]
        [HttpGet("{orderId}")]
        [ProducesResponseType(typeof(OrderDetailCustomResponse), 200)]
        public async Task<IActionResult> GetOrderDetailCustom(Guid orderId)
        {
            try
            {
                var result = await _orderService.GetOrderDetailCustomAsync(HttpContext, orderId);
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
            catch (Exception)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Lấy lịch sử cập nhật trạng thái của một đơn hàng.
        /// </summary>
        /// <param name="orderId">ID của đơn hàng cần lấy lịch sử.</param>
        /// <returns>
        /// Danh sách các bản ghi trạng thái dạng <see cref="OrderStatusHistoryItemResponse"/> gồm:
        /// - <c>StatusHistoryId</c>: Mã bản ghi trạng thái  
        /// - <c>Status</c>: Mã trạng thái (PENDING, CONFIRMED, etc.)  
        /// - <c>StatusDescription</c>: Mô tả trạng thái  
        /// - <c>Notes</c>: Ghi chú kèm theo (nếu có)  
        /// - <c>UpdatedBy</c>: Thông tin người cập nhật trạng thái  
        ///     - <c>UserId</c>, <c>FullName</c>, <c>PhoneNumber</c>
        /// - <c>CreatedAt</c>: Thời gian cập nhật  
        /// - <c>ContainMedia</c>: Có ảnh đính kèm trong trạng thái đó hay không
        /// </returns>
        /// <remarks>
        /// **Yêu cầu**: 
        /// 1) Đã đăng nhập (JWT).  
        /// 2) không chấp nhận đơn `"INCART"`
        /// 
        /// **Response codes**:
        /// - <c>200</c>: Lấy lịch sử thành công.
        /// - <c>404</c>: Không tìm thấy đơn hàng.
        /// - <c>401</c>: Token không hợp lệ hoặc không có quyền.
        /// - <c>500</c>: Lỗi hệ thống.
        /// </remarks>
        [HttpGet("history/{orderId}")]
        public async Task<IActionResult> GetOrderStatusHistory(Guid orderId)
        {
            try
            {
                var histories = await _orderService.GetOrderStatusHistoryAsync(HttpContext, orderId);
                return Ok(histories);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
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

        /// <summary>
        /// Lấy danh sách các đơn hàng đang trong giỏ (trạng thái "INCART") – dành cho Admin.
        /// </summary>
        /// <param name="page">Trang hiện tại (bắt đầu từ 1, mặc định = 1).</param>
        /// <param name="pageSize">Số bản ghi mỗi trang (mặc định = 10).</param>
        /// <remarks>
        /// **Yêu cầu**:  
        /// - Đã đăng nhập bằng JWT  
        /// - Có role là <c>Admin</c>  
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
        [Authorize(Roles = "Admin")]
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
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }
    }
}
