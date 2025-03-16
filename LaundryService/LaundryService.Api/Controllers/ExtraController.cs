using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/extras")]
    [ApiController]
    public class ExtraController : BaseApiController
    {
        private readonly IExtraService _extraService;

        public ExtraController(IExtraService extraService)
        {
            _extraService = extraService;
        }

        [HttpGet("{extraId}")]
        public async Task<IActionResult> GetById(Guid extraId)
        {
            try
            {
                var extra = await _extraService.GetExtraByIdAsync(extraId);
                return Ok(extra);
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

        [Authorize(Roles = "Admin")] // Chỉ Admin có quyền tạo Extra
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] CreateExtraRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _extraService.CreateExtraAsync(request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        [Authorize(Roles = "Admin")] // Chỉ Admin có quyền chỉnh sửa Extra
        [HttpPut]
        public async Task<IActionResult> Update([FromForm] UpdateExtraRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var updatedExtra = await _extraService.UpdateExtraAsync(request);
                return Ok(updatedExtra);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        [Authorize(Roles = "Admin")] // Chỉ Admin có quyền xóa Extra
        [HttpDelete("{extraId}")]
        public async Task<IActionResult> Delete(Guid extraId)
        {
            try
            {
                await _extraService.DeleteExtraAsync(extraId);
                return Ok(new { Message = "Extra deleted successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }
    }
}
