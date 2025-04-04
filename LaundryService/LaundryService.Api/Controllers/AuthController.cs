using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LaundryService.Api.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : BaseApiController
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Đăng ký (Register) tài khoản mới, đồng thời đăng nhập tự động
        /// </summary>
        /// <param name="request">
        ///     Thông tin đăng ký:
        ///     - <c>PhoneNumber</c>: Bắt buộc, định dạng 10 chữ số  
        ///     - <c>FullName</c>: Bắt buộc  
        ///     - <c>Password</c>: Bắt buộc, chứa tối thiểu 8 ký tự (1 hoa, 1 số, 1 đặc biệt)  
        ///     - <c>Dob</c>: Ngày sinh  
        ///     - <c>Gender</c>: Male/Female/Other
        /// </param>
        /// <param name="otpToken">
        ///     Mã OTP xác thực (lấy được khi user yêu cầu gửi OTP).  
        ///     Gửi kèm qua query string: <c>?otpToken=xxxx</c>.
        /// </param>
        /// <returns>Trả về <see cref="LoginResponse"/> chứa token nếu đăng ký thành công</returns>
        /// <remarks>
        /// **Quy trình**:  
        /// 1) Kiểm tra xem số điện thoại đã tồn tại chưa.  
        /// 2) Xác thực <c>otpToken</c> trong cache.  
        /// 3) Tạo user, hash password và lưu DB.  
        /// 4) Trả về thông tin đăng nhập (token, refresh token).  
        /// 
        /// **Response codes**:
        /// - **200**: Đăng ký và auto-login thành công.
        /// - **400**: Input sai định dạng hoặc OTP không hợp lệ, v.v.
        /// - **500**: Lỗi server.
        /// </remarks>
        [HttpPost("register")]
        [ProducesResponseType(typeof(LoginResponse), 200)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, [FromQuery] string otpToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var response = await _authService.RegisterAsync(request, otpToken);
                return Ok(response);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred" });
            }
        }

        /// <summary>
        /// Đăng nhập (Login) bằng số điện thoại và mật khẩu
        /// </summary>
        /// <param name="request">
        ///     <c>PhoneNumber</c>: Bắt buộc (10 chữ số),  
        ///     <c>Password</c>: Bắt buộc (khớp với password đã đăng ký)
        /// </param>
        /// <returns>
        /// Trả về <see cref="LoginResponse"/> nếu đăng nhập thành công (chứa Token, RefreshToken, Role,...)
        /// </returns>
        /// <remarks>
        /// **Response codes**:
        /// - **200**: Đăng nhập thành công.
        /// - **400**: Sai mật khẩu.
        /// - **404**: Không tìm thấy user theo <c>PhoneNumber</c>.
        /// - **500**: Lỗi server.
        /// </remarks>
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), 200)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var response = await _authService.LoginAsync(request);
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred" });
            }
        }

        /// <summary>
        /// Lấy mã <c>AccessToken</c> mới dựa trên <c>RefreshToken</c> cũ
        /// </summary>
        /// <param name="request">
        ///     <c>RefreshToken</c>: token cũ còn hạn
        /// </param>
        /// <returns>
        /// Trả về <c>AccessToken</c> và <c>RefreshToken</c> mới
        /// </returns>
        /// <remarks>
        /// **Response codes**:
        /// - **200**: Refresh thành công (trả về token mới).
        /// - **400**: RefreshToken không hợp lệ hoặc đã hết hạn.
        /// - **500**: Lỗi server.
        /// </remarks>
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var (accessToken, refreshToken) = await _authService.RefreshTokenAsync(request.RefreshToken);
                return Ok(new
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken
                });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred" });
            }
        }

        /// <summary>
        /// Đăng xuất (Logout) user hiện tại, vô hiệu hóa <c>RefreshToken</c> cũ
        /// </summary>
        /// <returns>Trả về thông báo logout thành công</returns>
        /// <remarks>
        /// **Yêu cầu**: Phải đăng nhập (Bearer token).  
        /// - Server sẽ xóa <c>RefreshToken</c> của user trong DB.  
        /// - AccessToken hiện tại cũng hết giá trị do không thể refresh nữa.  
        /// 
        /// **Response codes**:
        /// - **200**: Logout thành công
        /// - **401**: Token không hợp lệ
        /// - **404**: Không tìm thấy user
        /// - **400**: Lỗi khác (ApplicationException)
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                //Lấy UserId từ Token
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { Message = "Invalid token" });
                }

                await _authService.LogoutAsync(Guid.Parse(userId));
                return Ok(new { Message = "Logout successful" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred" });
            }
        }

        /// <summary>
        /// Đặt lại mật khẩu (Reset password) bằng OTP
        /// </summary>
        /// <param name="request">
        ///     <c>PhoneNumber</c>: Số điện thoại người dùng  
        ///     <c>NewPassword</c>: Mật khẩu mới  
        ///     <c>OtpToken</c>: Mã OTP để xác thực
        /// </param>
        /// <returns>Trả về thông báo đặt lại mật khẩu thành công</returns>
        /// <remarks>
        /// **Quy trình**:
        /// 1) Kiểm tra user theo <c>PhoneNumber</c>.  
        /// 2) Kiểm tra <c>OTP</c> trong cache.  
        /// 3) Cập nhật password mới (đã băm BCrypt).  
        /// 
        /// **Response codes**:
        /// - **200**: Đặt lại mật khẩu thành công
        /// - **400**: Sai OTP / User không tồn tại / Input không hợp lệ
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                await _authService.ResetPasswordAsync(request.PhoneNumber, request.NewPassword, request.OtpToken);
                return Ok(new { Message = "Password has been reset successfully." });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }
    }
}
