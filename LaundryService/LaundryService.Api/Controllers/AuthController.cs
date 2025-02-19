using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (request.Email == "test@gmail.com" && request.Password == "Password@1")
            {
                return Ok(new { Message = "Login successfully" });
            }

            return Unauthorized(new { Message = "Email or Password is not correct" });
        }
    }
}
