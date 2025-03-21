using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/extra-categories")]
    [ApiController]
    public class ExtraCategoryController : BaseApiController
    {
        private readonly IExtraCategoryService _extraCategoryService;

        public ExtraCategoryController(IExtraCategoryService extraCategoryService)
        {
            _extraCategoryService = extraCategoryService;
        }

        /// <summary>
        /// Lấy toàn bộ danh mục ExtraCategory, kèm danh sách Extras bên trong
        /// </summary>
        /// <returns>
        /// Danh sách (<see cref="ExtraCategoryDetailResponse"/>) chứa:
        /// - <c>ExtraCategoryId</c>, <c>Name</c>, <c>CreatedAt</c>
        /// - <c>Extras</c>: Danh sách Extra (ID, Name, Description, Price, ImageUrl, CreatedAt)
        /// </returns>
        /// <remarks>
        /// **Response codes**:
        /// - **200**: Lấy thành công
        /// - **500**: Lỗi server
        /// 
        /// Không yêu cầu đăng nhập.
        /// </remarks>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var extraCategories = await _extraCategoryService.GetAllExtraCategoriesAsync();
                return Ok(extraCategories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Tạo mới một ExtraCategory (chỉ Admin)
        /// </summary>
        /// <param name="request">
        /// Dữ liệu tạo mới:
        /// - <c>Name</c>: Tên của ExtraCategory (bắt buộc, không được trùng)
        /// </param>
        /// <returns>
        /// Trả về <see cref="ExtraCategoryResponse"/> nếu tạo thành công:
        /// - <c>ExtraCategoryId</c>, <c>Name</c>, <c>CreatedAt</c>
        /// </returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Phải đăng nhập với role = Admin  
        /// 
        /// **Response codes**:
        /// - **200**: Tạo thành công
        /// - **400**: Tên bị trùng hoặc input không hợp lệ
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin")] // Chỉ Admin có quyền tạo ExtraCategory
        [HttpPost]
        public async Task<IActionResult> CreateExtraCategory([FromBody] CreateExtraCategoryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var newCategory = await _extraCategoryService.CreateExtraCategoryAsync(request);
                return Ok(newCategory);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Xóa một ExtraCategory theo <c>id</c> (chỉ Admin)
        /// </summary>
        /// <param name="id">Id của ExtraCategory cần xóa</param>
        /// <returns>Trả về thông báo xóa thành công</returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Phải đăng nhập role = Admin  
        /// 
        /// **Logic**:
        /// - Không thể xóa nếu bên trong ExtraCategory còn <c>Extras</c>.
        /// - Ném <see cref="ApplicationException"/> nếu còn Extras liên kết.
        /// 
        /// **Response codes**:
        /// - **200**: Xóa thành công
        /// - **400**: Còn Extras liên quan hoặc request không hợp lệ
        /// - **404**: Không tìm thấy ExtraCategory
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin")] // Chỉ Admin có quyền xóa
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExtraCategory(Guid id)
        {
            try
            {
                await _extraCategoryService.DeleteExtraCategoryAsync(id);
                return Ok(new { Message = "Extra category deleted successfully." });
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
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }
    }
}
