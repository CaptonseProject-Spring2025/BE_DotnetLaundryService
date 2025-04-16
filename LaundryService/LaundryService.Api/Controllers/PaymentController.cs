using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Net.payOS;
using Net.payOS.Types;
using System.Net;

namespace LaundryService.Api.Controllers
{
    /// <summary>
    /// Controller quản lý các thao tác liên quan đến thanh toán (Payment).
    /// </summary>
    [Route("api/payments")]
    [ApiController]
    //[Authorize]
    public class PaymentController : BaseApiController
    {
        private readonly IPaymentService _paymentService;
        private readonly IConfiguration _configuration;

        public PaymentController(IPaymentService paymentService, IConfiguration configuration)
        {
            _paymentService = paymentService;
            _configuration = configuration;
        }

        /// <summary>
        /// Tạo mới một PaymentMethod (ví dụ: "PayOS", "MoMo", "VNPAY", ...)
        /// </summary>
        /// <param name="request">
        /// - <c>Name</c>: Tên phương thức thanh toán (bắt buộc, unique).  
        /// - <c>Description</c>: Mô tả (tùy chọn).  
        /// - <c>IsActive</c>: Trạng thái active (mặc định = true).
        /// </param>
        /// <remarks>
        /// **Yêu cầu**: Vai trò Admin.
        ///
        /// **Response codes**:
        /// - 200: Tạo thành công, trả về PaymentMethodResponse.
        /// - 400: Tên PaymentMethod đã tồn tại.
        /// - 401: Không có quyền (Chưa đăng nhập hoặc role không phải Admin).
        /// - 500: Lỗi server.
        /// </remarks>
        [HttpPost("methods")]
        [ProducesResponseType(typeof(PaymentMethodResponse), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreatePaymentMethod([FromBody] CreatePaymentMethodRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _paymentService.CreatePaymentMethodAsync(request);
                return Ok(result);
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
        /// Lấy tất cả PaymentMethods
        /// </summary>
        /// <returns>Danh sách PaymentMethodResponse</returns>
        [HttpGet("methods")]
        [ProducesResponseType(typeof(List<PaymentMethodResponse>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAllPaymentMethods()
        {
            try
            {
                var list = await _paymentService.GetAllPaymentMethodsAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Tạo link thanh toán PayOS cho Order. 
        /// </summary>
        /// <param name="request">
        ///  - <c>OrderId</c>  
        ///  - <c>Description</c>
        /// </param>
        /// <remarks>
        /// **Logic**:
        /// 1) Xác thực Order => lấy totalPrice, user => buyerName/phone  
        /// 2) Gọi PayOS tạo payment link  
        /// 3) Tạo record Payment trong DB (Transaction)  
        /// 4) Trả về data.checkoutUrl, data.qrCode, data.paymentLinkId, data.status  
        /// 
        /// **Response codes**:  
        /// - 200: Thành công  
        /// - 404: Không tìm thấy Order / PaymentMethod.  
        /// - 400: Lỗi logic (Order chưa có totalPrice,...)  
        /// - 500: Lỗi server/PayOS  
        /// </remarks>
        [HttpPost("payos/link")]
        [ProducesResponseType(typeof(CreatePayOSPaymentLinkResponse), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreatePayOSPaymentLink([FromBody] CreatePayOSPaymentLinkRequest request)
        {
            try
            {
                var result = await _paymentService.CreatePayOSPaymentLinkAsync(request);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                // Không tìm thấy order / user / paymethod
                return NotFound(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                // Lỗi logic 
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                // 500
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông tin link thanh toán PayOS từ paymentId.
        /// </summary>
        /// <param name="paymentId">PaymentId (Guid)</param>
        /// <remarks>
        /// **Logic**:
        /// 1) Tìm Payment theo paymentId -> parse metadata -> ra orderCode (long).
        /// 2) Gọi PayOS.getPaymentLinkInformation(orderCode).
        /// 3) Trả về toàn bộ thông tin PaymentLinkInformation (có convert thời gian sang VN).
        /// 
        /// **Response codes**:
        /// - 200: Thành công, trả về <see cref="PaymentLinkInfoResponse"/>.
        /// - 404: Không tìm thấy Payment / orderCode.
        /// - 400: Lỗi logic metadata / date parse.
        /// - 500: Lỗi server/PayOS.
        /// </remarks>
        [HttpGet("payos/info/{paymentId}")]
        [ProducesResponseType(typeof(PaymentLinkInfoResponse), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetPayOSPaymentLinkInfo(Guid paymentId)
        {
            try
            {
                var result = await _paymentService.GetPayOSPaymentLinkInfoAsync(paymentId);
                return Ok(result);
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

        ///// <summary>
        ///// Callback URL từ PayOS khi thanh toán xong.
        ///// </summary>
        //[HttpGet("payos/callback")]
        //public async Task<IActionResult> PayOSCallback(
        //    [FromQuery] string id,
        //    [FromQuery] string status,
        //    [FromQuery] bool cancel,
        //    [FromQuery] long orderCode
        //)
        //{
        //    try
        //    {
        //        // 1) Gọi service => update Paymentstatus
        //        var finalLink = await _paymentService.ConfirmPayOSCallbackAsync(id, status);

        //        // 2) Tuỳ: 
        //        //    - Trả JSON => client fetch => redirect
        //        //    - Hoặc 302 redirect server side
        //        // Ở đây ta làm JSON:
        //        return Ok(new
        //        {
        //            Message = "Cập nhật Payment thành công.",
        //            RedirectUrl = finalLink
        //        });
        //    }
        //    catch (KeyNotFoundException ex)
        //    {
        //        return NotFound(new { Message = ex.Message });
        //    }
        //    catch (ApplicationException ex)
        //    {
        //        return BadRequest(new { Message = ex.Message });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { Message = ex.Message });
        //    }
        //}

        /// <summary>
        /// Callback URL từ PayOS khi thanh toán xong.
        /// </summary>
        [HttpGet("payos/callback")]
        public async Task<IActionResult> PayOSCallback(
            [FromQuery] string id,
            [FromQuery] string status,
            [FromQuery] bool cancel,
            [FromQuery] long orderCode
        )
        {
            // Không xử lý DB nữa — Webhook đã lo.
            // Chỉ redirect về FE, kèm query string

            var AppUrl = "https://laundry.vuhai.me/";
            var redirectUrl = $"{AppUrl}?status={status}&orderCode={orderCode}";

            await Task.Delay(2000); // Dừng 2 giây

            return Redirect(redirectUrl); // 302 redirect về FE
        }

        /// <summary>
        /// [Webhook] Endpoint nhận thông báo cập nhật trạng thái thanh toán từ PayOS.
        /// </summary>
        /// <param name="webhookBody">Dữ liệu JSON được gửi từ PayOS.</param>
        /// <remarks>
        /// **Quan trọng:** Endpoint này KHÔNG yêu cầu xác thực người dùng (Authorize).
        /// Việc xác thực dựa trên signature của webhook được xử lý trong service.
        /// URL này cần được cấu hình trong trang quản trị của PayOS.
        ///
        /// **Logic**:
        /// 1. Nhận request POST từ PayOS.
        /// 2. Chuyển toàn bộ body cho `IPaymentService.HandlePayOSWebhookAsync`.
        /// 3. Service sẽ:
        ///    a. Xác thực signature.
        ///    b. Tìm Payment tương ứng.
        ///    c. Cập nhật Payment status, Order status (nếu PAID).
        ///    d. Lưu vào DB.
        /// 4. Trả về HTTP Status Code cho PayOS:
        ///    - 200 OK: Xử lý thành công (signature hợp lệ, DB update OK).
        ///    - 400 Bad Request: Signature không hợp lệ.
        ///    - 404 Not Found: Không tìm thấy Payment tương ứng (service có thể chọn bỏ qua thay vì trả 404).
        ///    - 500 Internal Server Error: Lỗi hệ thống trong quá trình xử lý hoặc cập nhật DB.
        /// </remarks>
        /// <response code="200">Webhook đã được nhận và xử lý thành công.</response>
        /// <response code="400">Dữ liệu webhook không hợp lệ (sai signature).</response>
        /// <response code="500">Lỗi server trong quá trình xử lý webhook.</response>
        [HttpPost("payos/webhook")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AllowAnonymous] // Webhook không cần user login
        public async Task<IActionResult> PayOSWebhook([FromBody] WebhookType webhookBody)
        {
            if (webhookBody == null || webhookBody.data == null || string.IsNullOrEmpty(webhookBody.signature))
            {
                return BadRequest(new { Message = "Invalid webhook payload." });
            }

            try
            {
                // 1) Gọi service để xử lý
                await _paymentService.HandlePayOSWebhookAsync(webhookBody);

                // 2) Trả về 200 OK (có thể trả object tuỳ ý, quan trọng là PayOS thấy status code 200)
                return Ok(new { Message = "Webhook processed successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                // Payment / orderCode / link not found
                return NotFound(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                // Lỗi logic (chữ ký sai, code != "00", v.v.)
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                // Các lỗi khác (lỗi DB, lỗi logic không mong muốn trong service)
                // Trả về 500 để PayOS biết có lỗi phía server và có thể thử gửi lại webhook sau
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Tích hợp Xác nhận Webhook URL
        /// </summary>
        [HttpPost("payos/confirm-webhook")]
        public async Task<IActionResult> ConfirmWebhookUrl([FromBody] string webhookUrl)
        {
            try
            {
                var clientId = _configuration["PayOS:ClientID"];
                var apiKey = _configuration["PayOS:APIKey"];
                var checksumKey = _configuration["PayOS:ChecksumKey"];
                var payOS = new PayOS(clientId, apiKey, checksumKey);

                var result = payOS.confirmWebhook(webhookUrl);

                return Ok(new { Message = "Webhook URL confirmed" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

    }
}
