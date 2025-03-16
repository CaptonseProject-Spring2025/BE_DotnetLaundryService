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
    }
}
