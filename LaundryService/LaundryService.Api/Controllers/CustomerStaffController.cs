using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Pagination;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [ApiController]
    [Route("api/customer-staff")]
    [Authorize(Roles = "Admin,CustomerStaff")]
    public class CustomerStaffController : BaseApiController
    {
        private readonly IOrderService _orderService;
        private readonly IFirebaseNotificationService _firebaseNotificationService;
        private readonly INotificationService _notificationService;

        public CustomerStaffController(IOrderService orderService, IFirebaseNotificationService firebaseNotificationService, INotificationService notificationService)
        {
            _orderService = orderService;
            _firebaseNotificationService = firebaseNotificationService;
            _notificationService = notificationService;
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
        [ProducesResponseType(typeof(PaginationResult<UserOrderResponse>), 200)]
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
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> ProcessOrder(string orderId)
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
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> ConfirmOrder([FromQuery] string orderId, [FromQuery] string? notes)
        {
            try
            {
                // Gọi service
                await _orderService.ConfirmOrderAsync(HttpContext, orderId, notes ?? "");

                // Lấy customerId để gửi notification
                var customerId = await _orderService.GetCustomerIdByOrderAsync(orderId);

                // Lưu notification vào DB
                try
                {
                    await _notificationService.CreateOrderConfirmedNotificationAsync(customerId, orderId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi tạo notification trong hệ thống: {ex.Message}");
                }

                // Gửi noti
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _firebaseNotificationService.SendOrderNotificationAsync(
                            customerId.ToString(),
                            NotificationType.OrderConfirmed
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi gửi thông báo OrderConfirmed: {ex.Message}");
                    }
                });

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

        /// <summary>
        /// Staff xác nhận hủy đơn hàng đang xử lý – trường hợp đã liên hệ với khách và được xác nhận hủy.
        /// </summary>
        /// <param name="assignmentId">Mã phân công xử lý đơn (bắt buộc).</param>
        /// <param name="notes">Ghi chú lý do hủy đơn (bắt buộc).</param>
        /// <returns>Thông báo hủy đơn thành công.</returns>
        /// <remarks>
        /// **Mục đích**:  
        /// Cho phép nhân viên gọi khách và xác nhận khách muốn **hủy đơn hàng**, sau đó cập nhật hệ thống.
        ///
        /// **Logic xử lý**:
        /// 1. Tìm `OrderAssignmentHistory` theo `assignmentId`.
        /// 2. Kiểm tra thời gian: nếu đã quá 30 phút kể từ `AssignedAt` => không cho phép hủy.
        /// 3. Nếu hợp lệ:
        ///     - Cập nhật `Order`:
        ///         - `CurrentStatus = "CANCELLED"`
        ///     - Thêm record vào `OrderStatusHistory`:  
        ///         - `Status = "CANCELLED"`  
        ///         - `StatusDescription = "Đơn hàng đã hủy."`  
        ///         - `Notes = ghi chú từ nhân viên`
        ///
        /// **Yêu cầu**:
        /// - Đăng nhập với role: `CustomerStaff`
        ///
        /// **Response codes**:
        /// - <c>200</c>: Hủy đơn thành công
        /// - <c>400</c>: Quá thời gian hoặc logic sai
        /// - <c>404</c>: Không tìm thấy assignment
        /// - <c>401</c>: Không có quyền
        /// - <c>500</c>: Lỗi hệ thống
        /// </remarks>
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

                // Lấy thông tin để tạo notification
                var customerId = await _orderService.GetCustomerIdByAssignmentAsync(assignmentId);
                var orderId = await _orderService.GetOrderIdByAssignmentAsync(assignmentId);


                // Lưu notification vào DB
                try
                {
                    await _notificationService.CreateOrderCanceledNotificationAsync(customerId, orderId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi tạo notification trong hệ thống: {ex.Message}");
                }

                // Gửi noti
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _firebaseNotificationService.SendOrderNotificationAsync(
                            customerId.ToString(),
                            NotificationType.OrderCancelled
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi gửi thông báo OrderCanceled: {ex.Message}");
                    }
                });

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

        /// <summary>
        /// Staff hủy xử lý đơn hàng – khi không thể tiếp tục xử lý (ví dụ: không liên hệ được khách hàng...).
        /// </summary>
        /// <param name="assignmentId">Mã phân công xử lý đơn (bắt buộc).</param>
        /// <param name="note">Lý do hủy xử lý (bắt buộc).</param>
        /// <returns>Thông báo hủy xử lý thành công.</returns>
        /// <remarks>
        /// **Mục đích**:  
        /// Khi nhân viên bấm "xử lý đơn" nhưng sau đó không thể tiếp tục (không gọi được khách, lý do cá nhân,...), họ có thể **thoát khỏi việc xử lý đơn**.
        ///
        /// **Logic xử lý**:
        /// 1. Tìm `OrderAssignmentHistory` bằng `assignmentId`.
        /// 2. Kiểm tra thời gian: nếu `DateTime.UtcNow - AssignedAt > 30 phút` => **không cho hủy**, trả lỗi.
        ///
        /// **Yêu cầu**:
        /// - Đăng nhập với role: `CustomerStaff`
        ///
        /// **Response codes**:
        /// - <c>200</c>: Hủy xử lý thành công
        /// - <c>400</c>: Quá 30 phút hoặc thiếu thông tin
        /// - <c>404</c>: Không tìm thấy assignment
        /// - <c>401</c>: Không có quyền
        /// - <c>500</c>: Lỗi hệ thống
        /// </remarks>
        [HttpPost("cancel-processing")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> CancelProcessing([FromQuery] Guid assignmentId, [FromQuery] string note)
        {
            // 1) Kiểm tra param
            if (assignmentId == Guid.Empty || string.IsNullOrWhiteSpace(note))
            {
                return BadRequest(new { Message = "assignmentId & note đều bắt buộc." });
            }

            try
            {
                await _orderService.CancelProcessingAsync(HttpContext, assignmentId, note);
                return Ok(new { Message = "Staff đã hủy xử lý đơn hàng thành công." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                // Quá 30p, hoặc logic sai
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
        /// CustomerStaff thêm sản phẩm vào giỏ hàng của một khách hàng khác.
        /// </summary>
        /// <param name="userId">Id của người dùng mà CustomerStaff muốn thêm sản phẩm vào giỏ hàng.</param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("add-to-cart")]
        public async Task<IActionResult> AddToCart(Guid userId, [FromBody] AddToCartRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _orderService.StaffAddToCartAsync(userId, request);
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
        /// <param name="userId">Id của người dùng mà CustomerStaff muốn lấy giỏ hàng.</param>
        /// <returns>
        ///     Trả về <see cref="CartResponse"/> gồm:
        ///     - <c>OrderId</c>  
        ///     - <c>Items</c>: danh sách các CartItemResponse  
        ///         - Mỗi item có <c>ServiceName</c>, <c>ServicePrice</c>, <c>Quantity</c>, Extras, <c>SubTotal</c>
        ///     - <c>EstimatedTotal</c>: tổng tạm tính của cart
        /// </returns>
        /// <remarks>
        /// **Response codes**:
        /// - **200**: Trả về giỏ hàng
        /// - **404**: Không tìm thấy cart (user chưa thêm gì)
        /// - **401**: Chưa đăng nhập hoặc token không hợp lệ
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpGet("cart")]
        [ProducesResponseType(typeof(CartResponse), 200)]
        public async Task<IActionResult> GetCart(Guid userId)
        {
            try
            {
                var result = await _orderService.GetCartAsync(userId);
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
        /// Cập nhật một mục trong giỏ hàng (cart), bao gồm số lượng và danh sách extras.
        /// </summary>
        /// <param name="userId">Id của người dùng mà CustomerStaff muốn cập nhật giỏ hàng.</param>
        /// <param name="request">
        ///     <see cref="UpdateCartItemRequest"/> gồm:
        ///     - <c>OrderItemId</c>: ID của mục giỏ hàng cần cập nhật (bắt buộc)  
        ///     - <c>Quantity</c>: Số lượng mới (bắt buộc). Nếu bằng 0 thì sẽ xóa mục này.  
        ///     - <c>ExtraIds</c>: Danh sách ID của các Extra (có thể rỗng)
        /// </param>
        /// <returns>
        ///     Trả về <see cref="CartResponse"/> sau khi cập nhật:
        ///     - <c>OrderId</c>, <c>Items</c>, <c>EstimatedTotal</c>
        /// </returns>
        /// <remarks>
        /// **Response codes**:
        /// - <c>200</c>: Cập nhật giỏ hàng thành công, trả về cart mới
        /// - <c>400</c>: Lỗi logic hoặc dữ liệu đầu vào không hợp lệ
        /// - <c>401</c>: Không có quyền truy cập
        /// - <c>404</c>: Không tìm thấy OrderItem hoặc giỏ hàng không còn
        /// - <c>500</c>: Lỗi hệ thống
        /// </remarks>
        [Authorize]
        [HttpPut("cart")]
        [ProducesResponseType(typeof(CartResponse), 200)]
        public async Task<IActionResult> UpdateCartItem(Guid userId, [FromBody] UpdateCartItemRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var updatedCart = await _orderService.UpdateCartItemAsync(userId, request);
                return Ok(updatedCart);
            }
            catch (KeyNotFoundException ex)
            {
                // Nếu orderItem không tồn tại, hoặc giỏ hàng không còn => NotFound
                return NotFound(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                // Log nếu cần
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// CustomerStaff tạo đơn hàng cho khách hàng.
        /// </summary>
        /// <param name="userId"> Id của khách hàng </param>
        /// <param name="request">
        ///     <see cref="PlaceOrderRequest"/> gồm:
        ///     - <c>DeliveryAddressId</c>: Guid địa chỉ trả đồ (của user)  
        ///     - <c>Deliverytime</c>: Thời gian user mong muốn trả đồ (tùy chọn)  
        ///     - <c>Shippingfee</c>, <c>Shippingdiscount</c>, <c>Applicablefee</c>, <c>Discount</c>, <c>Total</c>: Các chi phí liên quan, tổng tạm tính  
        ///     - <c>Note</c>: Ghi chú (tùy chọn)  
        ///     - <c>Createdat</c>: Thời gian cập nhật status (tùy chọn, mặc định = UtcNow)
        /// </param>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (có JWT).
        ///
        /// **Response codes**:
        /// - **200**: Đặt hàng thành công, chuyển trạng thái => "CHECKING"
        /// - **400**: Lỗi logic (tổng tiền không khớp, v.v.)
        /// - **401**: Chưa đăng nhập hoặc token không hợp lệ
        /// - **404**: Không tìm thấy order INCART hoặc không tìm thấy địa chỉ
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpPost("place-order")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> PlaceOrder(Guid userId, [FromBody] CusStaffPlaceOrderRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var orderId = await _orderService.CusStaffPlaceOrderAsync(HttpContext, userId, request);
                                
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
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"An unexpected error occurred. {ex.Message}" });
            }
        }
    }
}
