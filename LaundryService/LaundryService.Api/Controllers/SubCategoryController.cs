using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/subcategories")]
    [ApiController]
    public class SubCategoryController : BaseApiController
    {
        private readonly ISubCategoryService _subCategoryService;

        public SubCategoryController(ISubCategoryService subCategoryService)
        {
            _subCategoryService = subCategoryService;
        }

        [HttpGet("{categoryId}")]
        public async Task<IActionResult> GetAllByCategoryId(Guid categoryId)
        {
            var subcategories = await _subCategoryService.GetAllByCategoryIdAsync(categoryId);
            return Ok(subcategories);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSubCategoryRequest request)
        {
            try
            {
                var result = await _subCategoryService.CreateSubCategoryAsync(request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred" });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubCategoryRequest request)
        {
            try
            {
                var updatedSubCategory = await _subCategoryService.UpdateSubCategoryAsync(id, request);
                return Ok(updatedSubCategory);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred" });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _subCategoryService.DeleteSubCategoryAsync(id);
                return Ok(new { Message = "Subcategory deleted successfully." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred" });
            }
        }
    }
}
