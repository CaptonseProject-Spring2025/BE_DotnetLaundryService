using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/service-details")]
    [ApiController]
    public class ServiceDetailController : BaseApiController
    {
        private readonly IServiceDetailService _serviceDetailService;

        public ServiceDetailController(IServiceDetailService serviceDetailService)
        {
            _serviceDetailService = serviceDetailService;
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] CreateServiceDetailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _serviceDetailService.CreateServiceDetailAsync(request);
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
        [HttpPut]
        public async Task<IActionResult> Update([FromForm] UpdateServiceDetailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var updatedServiceDetail = await _serviceDetailService.UpdateServiceDetailAsync(request);
                return Ok(updatedServiceDetail);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{serviceId}")]
        public async Task<IActionResult> Delete(Guid serviceId)
        {
            try
            {
                await _serviceDetailService.DeleteServiceDetailAsync(serviceId);
                return Ok(new { Message = "Service detail deleted successfully." });
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
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("add-extras")]
        public async Task<IActionResult> AddExtras([FromBody] AddExtrasToServiceDetailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _serviceDetailService.AddExtrasToServiceDetailAsync(request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpGet("{serviceId}")]
        public async Task<IActionResult> GetById(Guid serviceId)
        {
            try
            {
                var serviceDetail = await _serviceDetailService.GetServiceDetailWithExtrasAsync(serviceId);
                return Ok(serviceDetail);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("remove-extras/{serviceId}")]
        public async Task<IActionResult> RemoveServiceExtras(Guid serviceId)
        {
            try
            {
                var result = await _serviceDetailService.DeleteServiceExtraMappingsAsync(serviceId);
                if (!result)
                {
                    return NotFound(new { Message = "No extra mappings found for this service." });
                }

                return Ok(new { Message = "All extra mappings removed successfully." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (ArgumentException ex)
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
