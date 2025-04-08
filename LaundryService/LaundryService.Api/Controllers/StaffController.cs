using LaundryService.Domain.Entities;
using LaundryService.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [ApiController]
    [Route("api/staff")]
    [Authorize(Roles = "Staff")]
    public class StaffController : BaseApiController
    {
        [HttpPost("upload-order-photos")]
        public async Task<IActionResult> UploadOrderPhotos([FromForm] Guid orderId, [FromForm] List<IFormFile> files)
        {
            if (files == null || !files.Any())
                return BadRequest("No files received.");

            //foreach (var file in files)
            //{
            //    // Validate file (optional): size, type,...
            //    // Save file lên Backblaze hoặc lưu local trước, rồi đẩy lên B2
            //    string fileUrl = await _b2StorageService.UploadFileAsync(file); // giả sử có service lưu file

            //    // Ghi thông tin ảnh vào DB
            //    var photo = new Orderphoto
            //    {
            //        Orderid = orderId,
            //        Photourl = fileUrl,
            //        Createdat = DateTime.UtcNow,
            //        // Có thể thêm DriverId nếu Staff gắn cứng vào driver
            //    };

            //    await _unitOfWork.Repository<Orderphoto>().InsertAsync(photo, saveChanges: false);
            //}

            //await _unitOfWork.SaveChangesAsync();
            return Ok("Uploaded successfully");
        }

    }
}
