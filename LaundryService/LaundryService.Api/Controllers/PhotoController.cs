using LaundryService.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/photos")]
    [ApiController]
    [Authorize]
    public class PhotoController : ControllerBase
    {
        private readonly IPhotoService _photoService;

        public PhotoController(IPhotoService photoService)
        {
            _photoService = photoService;
        }

        /// <summary>
        /// Lấy danh sách photoUrl cho 1 statusHistoryId
        /// </summary>
        /// <param name="statusHistoryId">Guid StatusHistoryId</param>
        /// <returns>200: Danh sách url, 404 nếu không thấy</returns>
        [HttpGet]
        public async Task<IActionResult> GetPhotosByStatusHistoryId([FromQuery] Guid statusHistoryId)
        {
            if (statusHistoryId == Guid.Empty)
            {
                return BadRequest(new { Message = "statusHistoryId is required." });
            }

            try
            {
                var photoUrls = await _photoService.GetPhotoUrlsByStatusHistoryIdAsync(statusHistoryId);
                return Ok(photoUrls);
            }
            catch (KeyNotFoundException ex)
            {
                // Nếu service ném lỗi => 404
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                // Lỗi khác => 500
                return StatusCode(500, new { Message = ex.Message });
            }
        }
    }
}
