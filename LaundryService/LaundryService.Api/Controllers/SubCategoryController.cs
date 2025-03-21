using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    /// <summary>
    /// Controller quản lý SubCategory (danh mục con) thuộc về Category
    /// </summary>
    [Route("api/subcategories")]
    [ApiController]
    public class SubCategoryController : BaseApiController
    {
        private readonly ISubCategoryService _subCategoryService;

        /// <summary>
        /// Khởi tạo SubCategoryController
        /// </summary>
        /// <param name="subCategoryService">Service xử lý logic cho SubCategory</param>
        public SubCategoryController(ISubCategoryService subCategoryService)
        {
            _subCategoryService = subCategoryService;
        }

        /// <summary>
        /// Lấy toàn bộ SubCategory thuộc về 1 Category
        /// </summary>
        /// <param name="categoryId">ID của Category</param>
        /// <returns>
        /// Danh sách <see cref="SubCategoryResponse"/>:
        /// - <c>SubCategoryId</c>
        /// - <c>Name</c>
        /// </returns>
        /// <remarks>
        /// **Response codes**:
        /// - **200**: Trả về danh sách subcategory  
        /// - **500**: Lỗi server (nếu có)
        /// 
        /// Không yêu cầu đăng nhập, tùy vào logic của bạn (có thể public).
        /// </remarks>
        [HttpGet("{categoryId}")]
        public async Task<IActionResult> GetAllByCategoryId(Guid categoryId)
        {
            var subcategories = await _subCategoryService.GetAllByCategoryIdAsync(categoryId);
            return Ok(subcategories);
        }

        /// <summary>
        /// Tạo mới 1 SubCategory (chỉ Admin)
        /// </summary>
        /// <param name="request">
        ///     <see cref="CreateSubCategoryRequest"/> gồm:
        ///     - <c>CategoryId</c>: Bắt buộc (ID category cha)  
        ///     - <c>Name</c>: Tên SubCategory (bắt buộc, không trùng)
        /// </param>
        /// <returns>
        /// Trả về <see cref="SubCategoryResponse"/> nếu tạo thành công
        /// </returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Role = "Admin"  
        /// 
        /// **Logic**:
        /// 1) Kiểm tra CategoryId có tồn tại không.  
        /// 2) Kiểm tra trùng tên SubCategory trong cùng Category.  
        /// 3) Tạo SubCategory mới.  
        /// 
        /// **Response codes**:
        /// - **200**: Tạo thành công
        /// - **400**: CategoryId không tồn tại / trùng tên / dữ liệu không hợp lệ
        /// - **401**: Chưa đăng nhập hoặc không phải Admin
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSubCategoryRequest request)
        {
            try
            {
                var result = await _subCategoryService.CreateSubCategoryAsync(request);
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

        /// <summary>
        /// Cập nhật thông tin 1 SubCategory (chỉ Admin)
        /// </summary>
        /// <param name="id">ID của SubCategory cần cập nhật</param>
        /// <param name="request">
        ///     <see cref="UpdateSubCategoryRequest"/> gồm:
        ///     - <c>Name</c>: Tên mới (tùy chọn, không trùng)
        /// </param>
        /// <returns>
        /// Trả về <see cref="SubCategoryResponse"/> sau khi update
        /// </returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Role = "Admin"  
        /// 
        /// **Logic**:
        /// 1) Tìm SubCategory theo <c>id</c>.  
        /// 2) Nếu có <c>Name</c> mới, kiểm tra trùng trong cùng Category.  
        /// 3) Cập nhật và lưu DB.  
        /// 
        /// **Response codes**:
        /// - **200**: Cập nhật thành công
        /// - **400**: SubCategory không tồn tại / trùng tên / dữ liệu không hợp lệ
        /// - **401**: Chưa đăng nhập hoặc không phải Admin
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubCategoryRequest request)
        {
            try
            {
                var updatedSubCategory = await _subCategoryService.UpdateSubCategoryAsync(id, request);
                return Ok(updatedSubCategory);
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

        /// <summary>
        /// Xóa 1 SubCategory (chỉ Admin)
        /// </summary>
        /// <param name="id">ID của SubCategory</param>
        /// <returns>Thông báo xóa thành công</returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Role = "Admin"  
        /// 
        /// **Logic**:
        /// 1) Tìm SubCategory theo <c>id</c>.  
        /// 2) Không xóa được nếu có ServiceDetail liên quan.  
        /// 
        /// **Response codes**:
        /// - **200**: Xóa thành công
        /// - **400**: SubCategory không tồn tại hoặc logic xóa không hợp lệ
        /// - **401**: Chưa đăng nhập hoặc không phải Admin
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _subCategoryService.DeleteSubCategoryAsync(id);
                return Ok(new { Message = "Subcategory deleted successfully." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred" });
            }
        }
    }
}
