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
    /// Controller quản lý các thao tác liên quan đến thanh toán (Payment).
    /// </summary>
    [Route("api/payments")]
    [ApiController]
    //[Authorize]
    public class PaymentController : BaseApiController
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
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
    }
}
