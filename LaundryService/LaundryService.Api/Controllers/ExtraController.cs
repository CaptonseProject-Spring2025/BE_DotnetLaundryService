using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/extras")]
    [ApiController]
    public class ExtraController : BaseApiController
    {
        private readonly IExtraService _extraService;

        public ExtraController(IExtraService extraService)
        {
            _extraService = extraService;
        }

        /// <summary>
        /// Lấy thông tin chi tiết 1 Extra qua ID
        /// </summary>
        /// <param name="extraId">ID của Extra</param>
        /// <returns>
        ///     Trả về <see cref="ExtraResponse"/> chứa:
        ///     - <c>ExtraId</c>, <c>Name</c>, <c>Description</c>, <c>Price</c>, <c>ImageUrl</c>, <c>CreatedAt</c>
        /// </returns>
        /// <remarks>
        /// **Response codes**:
        /// - **200**: Tìm thấy Extra
        /// - **404**: Không tìm thấy Extra
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpGet("{extraId}")]
        public async Task<IActionResult> GetById(Guid extraId)
        {
            try
            {
                var extra = await _extraService.GetExtraByIdAsync(extraId);
                return Ok(extra);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Tạo mới một Extra (chỉ Admin có quyền)
        /// </summary>
        /// <param name="request">
        /// Dữ liệu tạo Extra:
        /// - <c>ExtraCategoryId</c>: ID của ExtraCategory cha (bắt buộc, phải tồn tại)  
        /// - <c>Name</c>: Tên Extra (bắt buộc)  
        /// - <c>Description</c>: Mô tả (tùy chọn)  
        /// - <c>Price</c>: Giá (bắt buộc)  
        /// - <c>Image</c>: File ảnh (tùy chọn, gửi kèm <c>multipart/form-data</c>)
        /// </param>
        /// <returns>
        /// Trả về <see cref="ExtraResponse"/> nếu tạo thành công.
        /// </returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Role = Admin  
        /// 
        /// **Logic**:
        /// - Phải tìm thấy <c>ExtraCategory</c> tương ứng (<c>ExtraCategoryId</c>).
        /// - Nếu có <c>Image</c>, sẽ upload lên Backblaze B2.
        /// 
        /// **Response codes**:
        /// - **200**: Tạo thành công
        /// - **400**: Không tìm thấy ExtraCategory hoặc dữ liệu không hợp lệ
        /// - **500**: Lỗi server
        /// 
        /// **Lưu ý**: Gửi <c>Image</c> qua [FromForm] => request dạng `multipart/form-data`.
        /// </remarks>
        [Authorize(Roles = "Admin")] // Chỉ Admin có quyền tạo Extra
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] CreateExtraRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _extraService.CreateExtraAsync(request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Cập nhật thông tin Extra (chỉ Admin có quyền)
        /// </summary>
        /// <param name="request">
        /// Dữ liệu cập nhật:
        /// - <c>ExtraId</c>: Bắt buộc (định danh Extra cần update)  
        /// - <c>Name</c>: Tên mới (tùy chọn, không trùng trong cùng Category)  
        /// - <c>Description</c>, <c>Price</c>, <c>Image</c> (tùy chọn)
        /// </param>
        /// <returns>
        /// Trả về <see cref="ExtraResponse"/> sau khi cập nhật
        /// </returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Role = Admin  
        /// 
        /// **Logic**:
        /// - Không được để trùng <c>Name</c> trong cùng 1 <c>ExtraCategory</c>.
        /// - Nếu có upload <c>Image</c> mới, xóa ảnh cũ trên B2, upload ảnh mới.
        /// 
        /// **Response codes**:
        /// - **200**: Update thành công
        /// - **400**: Tên trùng hoặc dữ liệu sai
        /// - **500**: Lỗi server
        /// 
        /// **Lưu ý**: Gửi request qua <c>multipart/form-data</c> nếu có file ảnh.
        /// </remarks>
        [Authorize(Roles = "Admin")] // Chỉ Admin có quyền chỉnh sửa Extra
        [HttpPut]
        public async Task<IActionResult> Update([FromForm] UpdateExtraRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var updatedExtra = await _extraService.UpdateExtraAsync(request);
                return Ok(updatedExtra);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Xóa một Extra theo <c>extraId</c> (chỉ Admin có quyền)
        /// </summary>
        /// <param name="extraId">ID của Extra cần xóa</param>
        /// <returns>Trả về thông báo "Extra deleted successfully." nếu xóa thành công</returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Role = Admin  
        /// 
        /// **Logic**:
        /// - Nếu Extra đang được sử dụng trong OrderExtra hoặc ServiceExtraMapping => không cho xóa.
        /// 
        /// **Response codes**:
        /// - **200**: Xóa thành công
        /// - **400**: Có liên kết nên không thể xóa (InvalidOperationException)
        /// - **404**: Không tìm thấy Extra
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin")] // Chỉ Admin có quyền xóa Extra
        [HttpDelete("{extraId}")]
        public async Task<IActionResult> Delete(Guid extraId)
        {
            try
            {
                await _extraService.DeleteExtraAsync(extraId);
                return Ok(new { Message = "Extra deleted successfully." });
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
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }
    }
}
