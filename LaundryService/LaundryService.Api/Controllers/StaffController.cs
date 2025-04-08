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

        /// <summary>
        /// Staff nhận đơn giặt (đơn đang PICKEDUP) => chuyển trạng thái sang CHECKING.
        /// </summary>
        /// <param name="orderId">ID đơn hàng cần nhận.</param>
        /// <remarks>
        /// **Logic**:
        /// 1) Kiểm tra orderStatus == "PICKEDUP". Nếu không => 400
        /// 2) Cập nhật order => status = "CHECKING"
        /// 3) Tạo orderStatusHistory => Status="CHECKING"
        /// 4) Trả về message success
        /// </remarks>
        /// <response code="200">Đơn hàng nhận giặt thành công.</response>
        /// <response code="400">Đơn không ở trạng thái PICKEDUP.</response>
        /// <response code="404">Không tìm thấy đơn.</response>
        /// <response code="401">Không có quyền.</response>
        /// <response code="500">Lỗi server.</response>
        [HttpPost("orders/receive-for-check")]
        public async Task<IActionResult> ReceiveOrderForCheck([FromQuery] string orderId)
        {
            try
            {
                await _staffService.ReceiveOrderForCheckAsync(HttpContext, orderId);
                return Ok(new { Message = "Đã nhận đơn để kiểm tra/giặt (CHECKING) thành công." });
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
                return StatusCode(500, new { Message = ex.Message });
            }
        }
    }
}
