using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/firebase")]
    [ApiController]
    public class FirebaseController : BaseApiController
    {
        private readonly IFirebaseStorageService _firebaseStorageService;

        public FirebaseController(IFirebaseStorageService firebaseStorageService)
        {
            _firebaseStorageService = firebaseStorageService;
        }

        [HttpPost("save-fcmtoken")]
        public async Task<IActionResult> SaveToken([FromBody] CreateTokenRequest request)
        {
            if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.FcmToken))
            {
                return BadRequest("UserId và Token không được để trống.");
            }

            try
            {
                await _firebaseStorageService.SaveTokenAsync(request.UserId, request.FcmToken);
                return Ok("FCMToken đã được lưu thành công!");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi khi lưu fcmtoken: {ex.Message}");
            }
        }
    }
}