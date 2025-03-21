using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UserController : BaseApiController
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(Guid id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                return Ok(user);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        [HttpPost("check-phone")]
        public async Task<IActionResult> CheckPhoneNumberExists(string phone)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                if (await _userService.CheckPhoneNumberExistsAsync(phone))
                {
                    return Ok(new { Message = "Phone number is exists" });
                }
                return BadRequest(new { Message = "Phone number is not exists" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred" });
            }
        }

        [Authorize]
        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateUserProfile([FromForm] UpdateUserProfileRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var updatedUser = await _userService.UpdateUserProfileAsync(request);
                return Ok(updatedUser);
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
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            try
            {
                await _userService.DeleteUserAsync(id);
                return Ok(new { Message = "User deleted successfully." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery] string? role, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var users = await _userService.GetUsersAsync(HttpContext, role, page, pageSize);
                return Ok(users);
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
        /// Admin tạo mới 1 user
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("create")]
        public async Task<IActionResult> CreateUser([FromForm] CreateUserRequest request)
        {
            // Lưu ý: [FromForm] để upload file (Avatar) qua multipart/form-data
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var newUser = await _userService.CreateUserAsync(request);
                return Ok(newUser);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }
    }
}
