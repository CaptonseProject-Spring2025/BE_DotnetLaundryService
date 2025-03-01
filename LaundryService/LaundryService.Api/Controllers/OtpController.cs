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
