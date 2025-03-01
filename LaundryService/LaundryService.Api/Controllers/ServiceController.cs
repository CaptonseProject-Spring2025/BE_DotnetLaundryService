using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/categories")]
    [ApiController]
    public class ServiceController : BaseApiController
    {
        private readonly IServiceService _serviceService;

        public ServiceController(IServiceService serviceService)
        {
            _serviceService = serviceService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var categories = await _serviceService.GetAllAsync();
            return Ok(categories);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var category = await _serviceService.GetByIdAsync(id);
                return Ok(new
                {
                    CategoryId = category.Categoryid,
                    Name = category.Name,
                    Icon = category.Icon,
                    CreatedAt = category.Createdat
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateServiceCategory([FromForm] CreateServiceCategoryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var newCategory = await _serviceService.CreateServiceCategoryAsync(request);
                return Ok(new
                {
                    Message = "Service category created successfully.",
                    CategoryId = newCategory.Categoryid,
                    Name = newCategory.Name,
                    IconUrl = newCategory.Icon,
                    CreatedAt = newCategory.Createdat
                });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromForm] UpdateServiceCategoryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var updatedCategory = await _serviceService.UpdateServiceCategoryAsync(id, request);
            return Ok(updatedCategory);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _serviceService.DeleteAsync(id);
                return Ok(new { Message = "Service category deleted successfully." });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
        }
    }
}
