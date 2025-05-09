using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/categories")]
    [ApiController]
    public class CategoryController : BaseApiController
    {
        private readonly IServiceService _serviceService;

        public CategoryController(IServiceService serviceService)
        {
            _serviceService = serviceService;
        }

        /// <summary>
        /// Lấy toàn bộ danh sách danh mục (Service Category)
        /// </summary>
        /// <returns>
        /// Danh sách các danh mục dạng đối tượng ẩn danh:  
        /// {CategoryId, Name, Icon, CreatedAt}
        /// </returns>
        /// <remarks>
        /// **Response codes**:
        /// - **200**: Lấy thành công
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var categories = await _serviceService.GetAllAsync();
            return Ok(categories);
        }

        /// <summary>
        /// Lấy chi tiết một danh mục (Category) cùng các SubCategory và Service bên trong
        /// </summary>
        /// <param name="id">ID (Guid) của Category cần lấy</param>
        /// <returns>
        /// Trả về <see cref="CategoryDetailResponse"/> bao gồm:
        /// - <c>CategoryId</c>, <c>Name</c>, <c>Icon</c>
        /// - <c>SubCategories</c>: Danh sách SubCategory (mỗi SubCategory gồm <c>ServiceDetails</c>)
        /// </returns>
        /// <remarks>
        /// **Response codes**:
        /// - **200**: Lấy thành công
        /// - **404**: Không tìm thấy danh mục
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(CategoryDetailResponse), 200)]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var category = await _serviceService.GetCategoryDetailsAsync(id);
                return Ok(category);

                //var category = await _serviceService.GetByIdAsync(id);
                //return Ok(new
                //{
                //    CategoryId = category.Categoryid,
                //    Name = category.Name,
                //    Icon = category.Icon,
                //    CreatedAt = category.Createdat
                //});
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Tạo mới một danh mục (Category)
        /// </summary>
        /// <param name="request">
        /// Thông tin tạo mới danh mục:
        /// - <c>Name</c>: Tên danh mục (bắt buộc, không trùng)  
        /// - <c>Icon</c>: File ảnh tải lên (tùy chọn, gửi qua <c>multipart/form-data</c>)
        /// </param>
        /// <returns>Trả về đối tượng ẩn danh gồm Message, CategoryId, Name, IconUrl, CreatedAt</returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Phải đăng nhập **Role = Admin**.  
        /// 
        /// **Response codes**:
        /// - **200**: Tạo thành công
        /// - **400**: Tên danh mục trùng, hoặc dữ liệu không hợp lệ
        /// - **500**: Lỗi server
        /// 
        /// **Lưu ý**:
        /// - Nếu cần upload <c>Icon</c>, phải gửi request dạng `multipart/form-data`.
        /// </remarks>
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateServiceCategory([FromForm] CreateServiceCategoryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var newCategory = await _serviceService.CreateServiceCategoryAsync(request);
                return Ok(new
                {
                    Message = "Service category created successfully.",
                    CategoryId = newCategory.Categoryid,
                    Name = newCategory.Name,
                    IconUrl = newCategory.Icon,
                    CreatedAt = newCategory.Createdat
                });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật danh mục (Category) theo Id
        /// </summary>
        /// <param name="id">Id của danh mục cần cập nhật</param>
        /// <param name="request">
        /// Dữ liệu cập nhật:
        /// - <c>Name</c>: (tùy chọn)  
        /// - <c>Icon</c>: (tùy chọn, file upload nếu muốn thay icon cũ)
        /// </param>
        /// <returns>Trả về <see cref="Servicecategory"/> sau khi update</returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Role = Admin.  
        /// 
        /// **Response codes**:
        /// - **200**: Update thành công
        /// - **400**: Thông tin không hợp lệ
        /// - **404**: Không tìm thấy Category
        /// - **500**: Lỗi server
        /// 
        /// **Lưu ý**: Nếu upload icon mới, request phải là `multipart/form-data`.
        /// </remarks>
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromForm] UpdateServiceCategoryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var updatedCategory = await _serviceService.UpdateServiceCategoryAsync(id, request);
            return Ok(updatedCategory);
        }

        /// <summary>
        /// Xóa một danh mục (Category) theo <c>id</c>
        /// </summary>
        /// <param name="id">Id của danh mục cần xóa</param>
        /// <returns>Trả về thông báo xóa thành công</returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Role = Admin.  
        /// 
        /// **Lưu ý**:
        /// - Nếu danh mục còn Sub-services liên kết thì không thể xóa (throws ApplicationException).
        /// - Icon của danh mục cũng sẽ bị xóa khỏi lưu trữ.
        /// 
        /// **Response codes**:
        /// - **200**: Xóa thành công
        /// - **400**: Có sub-services liên quan hoặc request sai
        /// - **404**: Không tìm thấy category
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _serviceService.DeleteAsync(id);
                return Ok(new { Message = "Service category deleted successfully." });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
        }

        //[Authorize(Roles = "Admin")]
        [HttpDelete("cascade/{id}")]
        public async Task<IActionResult> DeleteCascade(Guid id)
        {
            try
            {
                await _serviceService.DeleteCategoryCascadeAsync(id);
                return Ok(new { Message = "Service category and all related data deleted successfully." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }
}
