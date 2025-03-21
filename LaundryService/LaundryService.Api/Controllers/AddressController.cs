using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/addresses")]
    [ApiController]
    [Authorize]
    public class AddressController : BaseApiController
    {
        private readonly IAddressService _addressService;

        public AddressController(IAddressService addressService)
        {
            _addressService = addressService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateAddress([FromBody] CreateAddressRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { Message = "Invalid input data", Errors = ModelState });
            }

            try
            {
                var result = await _addressService.CreateAddressAsync(HttpContext, request);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred.", Error = ex.Message });
            }
        }

        [HttpDelete("{addressId}")]
        public async Task<IActionResult> DeleteAddress(Guid addressId)
        {
            try
            {
                var result = await _addressService.DeleteAddressAsync(HttpContext, addressId);
                if (result)
                {
                    return Ok(new { Message = "Address deleted successfully." });
                }
                return BadRequest(new { Message = "Failed to delete address." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred.", Error = ex.Message });
            }
        }
    }
}
