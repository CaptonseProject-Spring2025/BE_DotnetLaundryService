using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OtpController : ControllerBase
    {
        private readonly ISpeedSmsService _smsService;

        public OtpController(ISpeedSmsService smsService)
        {
            _smsService = smsService;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendOTP([FromBody] SendOtpRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
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
                bool isVerified = await _smsService.VerifyOTP(request.Phone, request.OTP);
                if (isVerified)
                {
                    return Ok(new { Message = "OTP verified successfully" });
                }
                else
                {
                    return Unauthorized(new { Message = "Invalid OTP" });
                }
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
