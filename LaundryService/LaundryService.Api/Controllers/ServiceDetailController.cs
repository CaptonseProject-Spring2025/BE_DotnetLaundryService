using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/service-details")]
    [ApiController]
    public class ServiceDetailController : BaseApiController
    {
        private readonly IServiceDetailService _serviceDetailService;

        public ServiceDetailController(IServiceDetailService serviceDetailService)
        {
            _serviceDetailService = serviceDetailService;
        }

        /// <summary>
        /// Tạo mới một ServiceDetail (chỉ Admin)
        /// </summary>
        /// <param name="request">
        ///     <see cref="CreateServiceDetailRequest"/> gồm:
        ///     - <c>SubCategoryId</c>: Id của Subservice (bắt buộc, phải tồn tại)  
        ///     - <c>Name</c>: Tên ServiceDetail (bắt buộc, không trùng trong Subservice)  
        ///     - <c>Description</c>, <c>Price</c> (tùy chọn)  
        ///     - <c>Image</c>: File ảnh (tùy chọn, upload multipart/form-data)
        /// </param>
        /// <returns>
        ///     Trả về <see cref="ServiceDetailResponse"/> nếu tạo thành công
        /// </returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Role = Admin  
        /// 
        /// **Logic**:
        /// 1) Kiểm tra Subservice tồn tại.  
        /// 2) Kiểm tra trùng <c>Name</c> trong Subservice.  
        /// 3) Upload <c>Image</c> nếu có.  
        /// 4) Tạo ServiceDetail.  
        /// 
        /// **Response codes**:
        /// - **200**: Tạo thành công
        /// - **400**: Dữ liệu không hợp lệ (trùng name, SubserviceId không tồn tại, v.v.)
        /// - **401**: Chưa đăng nhập hoặc không phải Admin
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] CreateServiceDetailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _serviceDetailService.CreateServiceDetailAsync(request);
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
        /// Cập nhật thông tin một ServiceDetail (chỉ Admin)
        /// </summary>
        /// <param name="request">
        ///     <see cref="UpdateServiceDetailRequest"/> gồm:
        ///     - <c>ServiceId</c>: Bắt buộc, ID của ServiceDetail  
        ///     - <c>Name</c>, <c>Description</c>, <c>Price</c>, <c>Image</c> (tùy chọn)
        /// </param>
        /// <returns>
        ///     Trả về <see cref="ServiceDetailResponse"/> sau khi cập nhật
        /// </returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Role = Admin  
        /// 
        /// **Logic**:
        /// 1) Kiểm tra ServiceDetail tồn tại.  
        /// 2) Nếu thay đổi <c>Name</c>, kiểm tra trùng name trong cùng Subservice.  
        /// 3) Upload <c>Image</c> mới, xóa <c>Image</c> cũ nếu có.  
        /// 
        /// **Response codes**:
        /// - **200**: Cập nhật thành công
        /// - **400**: Dữ liệu không hợp lệ (ServiceId sai, trùng Name, v.v.)
        /// - **401**: Chưa đăng nhập hoặc không phải Admin
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin")]
        [HttpPut]
        public async Task<IActionResult> Update([FromForm] UpdateServiceDetailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var updatedServiceDetail = await _serviceDetailService.UpdateServiceDetailAsync(request);
                return Ok(updatedServiceDetail);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Xóa một ServiceDetail (chỉ Admin)
        /// </summary>
        /// <param name="serviceId">ID của ServiceDetail cần xóa</param>
        /// <returns>Trả về thông báo xóa thành công</returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Role = Admin  
        /// 
        /// **Logic**:
        /// 1) Không xóa được nếu ServiceDetail đang có OrderItem, Ratings, hoặc Extras mapping.  
        /// 2) Nếu có Image => xóa Image khỏi B2.  
        /// 3) Xóa ServiceDetail khỏi DB.
        /// 
        /// **Response codes**:
        /// - **200**: Xóa thành công
        /// - **400**: ServiceDetail đang liên kết với OrderItem, Rating, Extras
        /// - **404**: Không tìm thấy ServiceDetail
        /// - **401**: Chưa đăng nhập hoặc không phải Admin
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{serviceId}")]
        public async Task<IActionResult> Delete(Guid serviceId)
        {
            try
            {
                await _serviceDetailService.DeleteServiceDetailAsync(serviceId);
                return Ok(new { Message = "Service detail deleted successfully." });
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
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Thêm danh sách Extras vào một ServiceDetail (chỉ Admin)
        /// </summary>
        /// <param name="request">
        ///     <see cref="AddExtrasToServiceDetailRequest"/> gồm:
        ///     - <c>ServiceId</c>: ID của ServiceDetail  
        ///     - <c>ExtraIds</c>: List ID của Extras cần thêm  
        /// </param>
        /// <returns>
        ///     Trả về <see cref="AddExtrasToServiceDetailResponse"/> mô tả số lượng thành công/thất bại, IDs thất bại
        /// </returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Role = Admin  
        /// 
        /// **Logic**:
        /// 1) Kiểm tra ServiceDetail tồn tại.  
        /// 2) Kiểm tra trong <c>ExtraIds</c> => những Extra nào tồn tại, những ExtraId nào không hợp lệ.  
        /// 3) Với những Extra chưa mapping, tạo <c>ServiceExtraMapping</c> mới.  
        /// 
        /// **Response codes**:
        /// - **200**: Thêm thành công (ít nhất 1 Extra)
        /// - **400**: Dữ liệu không hợp lệ (ServiceDetail không tồn tại, Extra không tồn tại,...)
        /// - **401**: Chưa đăng nhập hoặc không phải Admin
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin")]
        [HttpPost("add-extras")]
        public async Task<IActionResult> AddExtras([FromBody] AddExtrasToServiceDetailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _serviceDetailService.AddExtrasToServiceDetailAsync(request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật (thay thế) toàn bộ danh sách Extras của một ServiceDetail (chỉ Admin)
        /// </summary>
        /// <param name="request">
        ///     <see cref="AddExtrasToServiceDetailRequest"/> gồm:
        ///     - <c>ServiceId</c>: ID của ServiceDetail  
        ///     - <c>ExtraIds</c>: Danh sách ID Extras mới, sẽ thay thế danh sách cũ
        /// </param>
        /// <returns>
        ///     Trả về <see cref="AddExtrasToServiceDetailResponse"/> mô tả số lượng thành công/thất bại, IDs thất bại
        /// </returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Role = Admin  
        /// 
        /// **Logic**:
        /// 1) Xóa hết mapping Extras cũ của ServiceDetail.  
        /// 2) Thêm mapping mới cho tất cả <c>ExtraIds</c>.  
        /// 
        /// **Response codes**:
        /// - **200**: Cập nhật thành công
        /// - **400**: Dữ liệu sai (ServiceDetail không tồn tại, Extra không tồn tại, ...)
        /// - **401**: Chưa đăng nhập hoặc không phải Admin
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin")]
        [HttpPut("update-extras")]
        public async Task<IActionResult> UpdateServiceExtras([FromBody] AddExtrasToServiceDetailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _serviceDetailService.UpdateExtrasToServiceDetailAsync(request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Lấy thông tin chi tiết 1 ServiceDetail (cùng danh sách Extras theo từng ExtraCategory)
        /// </summary>
        /// <param name="serviceId">ID của ServiceDetail</param>
        /// <returns>
        ///     Trả về <see cref="ServiceDetailWithExtrasResponse"/> mô tả:  
        ///     - <c>Name</c>, <c>Description</c>, <c>Price</c>, <c>ImageUrl</c>, <c>CreatedAt</c>  
        ///     - <c>ExtraCategories</c>: các nhóm Extra (ExtraCategoryId, CategoryName, v.v.)
        /// </returns>
        /// <remarks>
        /// **Response codes**:
        /// - **200**: Tìm thấy ServiceDetail
        /// - **404**: Không tìm thấy ServiceDetail
        /// - **500**: Lỗi server
        /// 
        /// **Không yêu cầu**: đăng nhập (tùy logic), nếu bạn muốn công khai cho khách xem.
        /// </remarks>
        [HttpGet("{serviceId}")]
        public async Task<IActionResult> GetById(Guid serviceId)
        {
            try
            {
                var serviceDetail = await _serviceDetailService.GetServiceDetailWithExtrasAsync(serviceId);
                return Ok(serviceDetail);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Xóa toàn bộ Extra mappings của 1 ServiceDetail (chỉ Admin)
        /// </summary>
        /// <param name="serviceId">ID của ServiceDetail</param>
        /// <returns>Thông báo xóa thành công hoặc lỗi</returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Role = Admin  
        /// 
        /// **Logic**:
        /// 1) Tìm ServiceDetail => nếu không có => 400 hoặc 404.  
        /// 2) Tìm tất cả <c>ServiceExtraMapping</c> => xóa hết.  
        /// 3) Nếu không tìm thấy mapping => trả về 404.  
        /// 
        /// **Response codes**:
        /// - **200**: Xóa thành công
        /// - **400**: (nếu code bạn throw ra ArgumentException)  
        /// - **404**: ServiceDetail không có mappings
        /// - **401**: Chưa đăng nhập/không phải Admin
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin")]
        [HttpDelete("remove-extras/{serviceId}")]
        public async Task<IActionResult> RemoveServiceExtras(Guid serviceId)
        {
            try
            {
                var result = await _serviceDetailService.DeleteServiceExtraMappingsAsync(serviceId);
                if (!result)
                {
                    return NotFound(new { Message = "No extra mappings found for this service." });
                }

                return Ok(new { Message = "All extra mappings removed successfully." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Tìm kiếm 1 ServiceDetail theo Name (không phân biệt HOA/thường).
        /// </summary>
        /// <param name="name">Tên service cần tìm</param>
        /// <returns><see cref="ServiceDetailResponse"/> nếu tìm thấy</returns>
        [HttpGet("search")]
        public async Task<IActionResult> SearchByName([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { Message = "Query parameter 'name' is required." });

            try
            {
                var result = await _serviceDetailService.SearchServiceDetailByNameAsync(name);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

    }
}
