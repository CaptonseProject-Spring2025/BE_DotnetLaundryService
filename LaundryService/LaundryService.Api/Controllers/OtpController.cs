using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/otp")]
    [ApiController]
    public class OtpController : ControllerBase
    {
        private readonly ISpeedSmsService _smsService;
        private readonly IAuthService _authService;

        public OtpController(ISpeedSmsService smsService, IAuthService authService)
        {
            _smsService = smsService;
            _authService = authService;
        }

        /// <summary>
        /// Gửi mã OTP đến số điện thoại (dùng trong trường hợp đăng ký tài khoản)
        /// </summary>
        /// <param name="request">
        ///     <see cref="SendOtpRequest"/> chứa:
        ///     - <c>Phone</c>: Số điện thoại nhận OTP
        /// </param>
        /// <returns>
        ///     Trả về object chứa kết quả gửi SMS, ví dụ: <c>{ status, message, ... }</c>
        /// </returns>
        /// <remarks>
        /// **Logic**:
        /// 1) Kiểm tra tính hợp lệ của dữ liệu (ModelState).  
        /// 2) Check số điện thoại đã được đăng ký chưa.  
        ///     - Nếu **đã đăng ký** => trả về **400** với message: "Phone number is already registered."  
        /// 3) Nếu hợp lệ => gọi <c>_smsService.SendOTP</c> và trả về kết quả.  
        /// 
        /// **Response codes**:
        /// - **200**: Gửi OTP thành công (trả về thông tin từ SpeedSMS).
        /// - **400**: Dữ liệu không hợp lệ hoặc số điện thoại đã tồn tại.
        /// - **500**: Lỗi server hoặc lỗi chung khác.
        /// </remarks>
        [HttpPost("send")]
        public async Task<IActionResult> SendOTP([FromBody] SendOtpRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (await _authService.CheckPhoneNumberExistsAsync(request.Phone))
            {
                return BadRequest(new { Message = "Phone number is already registered." });
            }

            try
            {
                var response = await _smsService.SendOTP(request.Phone);
                return Ok(response);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred: " + ex.Message });
            }
        }

        /// <summary>
        /// Gửi mã OTP đến số điện thoại (dùng trong trường hợp reset password)
        /// </summary>
        /// <param name="request">
        ///     <see cref="SendOtpRequest"/> chứa:
        ///     - <c>Phone</c>: Số điện thoại nhận OTP
        /// </param>
        /// <returns>
        ///     Trả về object chứa kết quả gửi SMS, ví dụ: <c>{ status, message, ... }</c>
        /// </returns>
        /// <remarks>
        /// **Logic**:
        /// 1) Kiểm tra ModelState.  
        /// 2) Check số điện thoại **chưa đăng ký** => trả về **400** "Phone number is not registered."  
        /// 3) Nếu đã đăng ký => gọi <c>_smsService.SendOTP</c> và trả về kết quả.  
        /// 
        /// **Response codes**:
        /// - **200**: Gửi OTP thành công (trả về kết quả SpeedSMS).
        /// - **400**: Dữ liệu không hợp lệ, hoặc số điện thoại chưa đăng ký => không thể reset password.
        /// - **500**: Lỗi server hoặc lỗi chung khác.
        /// </remarks>
        [HttpPost("reset-password")]
        public async Task<IActionResult> SendOTPResetPass([FromBody] SendOtpRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!await _authService.CheckPhoneNumberExistsAsync(request.Phone))
            {
                return BadRequest(new { Message = "Phone number is not registered." });
            }

            try
            {
                var response = await _smsService.SendOTP(request.Phone);
                return Ok(response);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred: " + ex.Message });
            }
        }

        /// <summary>
        /// Gửi lại (re-send) mã OTP đến điện thoại (nếu chưa hết hạn)
        /// </summary>
        /// <param name="request">
        ///     <see cref="ResendOtpRequest"/> chứa:
        ///     - <c>Phone</c>: Số điện thoại cần gửi lại OTP
        /// </param>
        /// <returns>
        ///     Trả về object chứa kết quả gửi SMS
        /// </returns>
        /// <remarks>
        /// **Logic**:
        /// 1) Xóa OTP cũ trong cache (nếu còn).  
        /// 2) Gửi OTP mới và lưu lại cache.  
        /// 
        /// **Response codes**:
        /// - **200**: Gửi lại OTP thành công
        /// - **400**: Dữ liệu sai hoặc logic khác (ApplicationException)
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpPost("resend")]
        public async Task<IActionResult> ResendOTP([FromBody] ResendOtpRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var response = await _smsService.ResendOTP(request.Phone);
                return Ok(response);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred: " + ex.Message });
            }
        }

        /// <summary>
        /// Xác thực (verify) mã OTP đã được gửi trước đó
        /// </summary>
        /// <param name="request">
        ///     <see cref="VerifyOtpRequest"/> chứa:
        ///     - <c>Phone</c>: số điện thoại
        ///     - <c>OTP</c>: mã OTP cần xác thực
        /// </param>
        /// <returns>
        ///     Trả về object: <c>{ Message = "OTP verified successfully", Token = "..." }</c>
        /// </returns>
        /// <remarks>
        /// **Logic**:
        /// 1) Kiểm tra OTP với cache (nếu match => xóa OTP trong cache).  
        /// 2) Sinh ra 1 token tạm (có hạn 5 phút) và lưu vào cache: <c>"token_{phone}"</c>.  
        /// 3) Gửi token này về client, client dùng nó để gọi <c>/auth/register</c> hoặc <c>reset-password</c>.  
        /// 
        /// **Response codes**:
        /// - **200**: OTP xác thực thành công, trả về token tạm
        /// - **400**: OTP sai hoặc hết hạn
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpPost("verify")]
        public async Task<IActionResult> VerifyOTP([FromBody] VerifyOtpRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                string token = await _smsService.VerifyOTPAndGenerateToken(request.Phone, request.OTP);
                return Ok(new { Message = "OTP verified successfully", Token = token });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred: " + ex.Message });
            }
        }
    }
}
