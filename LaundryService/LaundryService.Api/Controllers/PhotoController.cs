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

        /// <summary>
        /// Xóa ảnh theo photoUrl.
        /// </summary>
        /// <param name="photoUrl">URL ảnh cần xóa</param>
        /// <returns>Xóa thành công => 200, nếu không => 404 / 500</returns>
        [HttpDelete]
        public async Task<IActionResult> DeletePhotoByUrl([FromQuery] string photoUrl)
        {
            if (string.IsNullOrWhiteSpace(photoUrl))
                return BadRequest(new { Message = "photoUrl is required." });

            try
            {
                await _photoService.DeletePhotoByUrlAsync(photoUrl);
                return Ok(new { Message = "Photo deleted successfully." });
            }
            catch (KeyNotFoundException ex)
            {
                // Không tìm thấy record => 404
                return NotFound(new { Message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                // Lỗi logic xóa S3 => 500
                return StatusCode(500, new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                // Lỗi bất ngờ => 500
                return StatusCode(500, new { Message = ex.Message });
            }
        }
    }
}
