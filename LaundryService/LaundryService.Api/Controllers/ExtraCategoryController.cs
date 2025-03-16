using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/extra-categories")]
    [ApiController]
    public class ExtraCategoryController : BaseApiController
    {
        private readonly IExtraCategoryService _extraCategoryService;

        public ExtraCategoryController(IExtraCategoryService extraCategoryService)
        {
            _extraCategoryService = extraCategoryService;
        }

        [Authorize(Roles = "Admin")] // Chỉ Admin có quyền tạo ExtraCategory
        [HttpPost]
        public async Task<IActionResult> CreateExtraCategory([FromBody] CreateExtraCategoryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var newCategory = await _extraCategoryService.CreateExtraCategoryAsync(request);
                return Ok(newCategory);
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

        [Authorize(Roles = "Admin")] // Chỉ Admin có quyền xóa
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExtraCategory(Guid id)
        {
            try
            {
                await _extraCategoryService.DeleteExtraCategoryAsync(id);
                return Ok(new { Message = "Extra category deleted successfully." });
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
    }
}
