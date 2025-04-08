using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Responses;
using LaundryService.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [ApiController]
    [Route("api/staff")]
    [Authorize(Roles = "Staff,Admin")]
    public class StaffController : BaseApiController
    {
        private readonly IStaffService _staffService;

        public StaffController(IStaffService staffService)
        {
            _staffService = staffService;
        }

        /// <summary>
        /// Lấy danh sách các đơn đã PICKEDUP cho staff Checking
        /// </summary>
        /// <returns>Danh sách <see cref="PickedUpOrderResponse"/>.</returns>
        /// <remarks>
        /// **Yêu cầu**: Đăng nhập với vai trò Staff hoặc Admin
        /// 
        /// **Logic**:
        /// 1) Tìm Order.Currentstatus="PICKEDUP"
        /// 2) Phải có ít nhất 1 OrderAssignmentHistory với Status="PICKUP_SUCCESS"
        /// 3) Sort: Emergency = true trước, sau đó theo DeliveryTime gần nhất.
        ///   - Emergency DESC
        ///   - DeliveryTime ASC
        /// </remarks>
        [HttpGet("orders/pickedup")]
        [ProducesResponseType(typeof(List<PickedUpOrderResponse>), 200)]
        public async Task<IActionResult> GetPickedUpOrders()
        {
            try
            {
                var result = await _staffService.GetPickedUpOrdersAsync(HttpContext);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log...
                return StatusCode(500, new { Message = ex.Message });
            }
        }

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
