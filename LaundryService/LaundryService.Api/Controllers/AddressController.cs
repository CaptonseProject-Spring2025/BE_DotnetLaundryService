using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    /// <summary>
    /// Controller quản lý địa chỉ (Address) của người dùng
    /// </summary>
    [Route("api/addresses")]
    [ApiController]
    [Authorize]
    public class AddressController : BaseApiController
    {
        private readonly IAddressService _addressService;

        public AddressController(IAddressService addressService)
        {
            _addressService = addressService;
        }

        /// <summary>
        /// Tạo mới một địa chỉ cho người dùng hiện tại
        /// </summary>
        /// <param name="request">
        /// Các thông tin tạo địa chỉ:
        /// - <c>DetailAddress</c>: địa chỉ chi tiết (bắt buộc)  
        /// - <c>Latitude</c>, <c>Longitude</c>: tọa độ do user cung cấp  
        /// - <c>AddressLabel</c>: nhãn địa chỉ (nhà, công ty, v.v.)  
        /// - <c>ContactName</c>, <c>ContactPhone</c>: Tên/điện thoại liên hệ (nếu khác so với user)  
        /// - <c>Description</c>: Thông tin mô tả địa chỉ
        /// </param>
        /// <returns>Trả về đối tượng <see cref="AddressResponse"/> mô tả địa chỉ vừa tạo</returns>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (token).  
        /// 
        /// Hệ thống sẽ kiểm tra tọa độ (lat, lng) so với địa chỉ (DetailAddress) thông qua Mapbox:
        ///  - Nếu sai lệch quá 1km, trả về lỗi.  
        ///  
        /// **Response codes**:
        /// - **200**: Tạo thành công, trả về địa chỉ mới
        /// - **400**: Input không hợp lệ (thiếu DetailAddress, hoặc tọa độ sai lệch, v.v.)
        /// - **401**: Chưa đăng nhập/token không hợp lệ
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpPost]
        public async Task<IActionResult> CreateAddress([FromBody] CreateAddressRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { Message = "Invalid input data", Errors = ModelState });
            }

            try
            {
                var result = await _addressService.CreateAddressAsync(HttpContext, request);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred.", Error = ex.Message });
            }
        }

        /// <summary>
        /// Xóa một địa chỉ dựa theo <c>addressId</c> (thuộc sở hữu của user hiện tại)
        /// </summary>
        /// <param name="addressId">Id của địa chỉ cần xóa</param>
        /// <returns>Trả về thông báo "Address deleted successfully." nếu xóa thành công</returns>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (token).  
        /// **Chỉ xóa địa chỉ nếu <c>UserId</c> trùng với địa chỉ đó**.  
        /// 
        /// **Response codes**:
        /// - **200**: Xóa thành công
        /// - **400**: Xóa thất bại (không xác định)
        /// - **401**: Chưa đăng nhập/token không hợp lệ
        /// - **404**: Không tìm thấy địa chỉ thuộc user
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpDelete("{addressId}")]
        public async Task<IActionResult> DeleteAddress(Guid addressId)
        {
            try
            {
                var result = await _addressService.DeleteAddressAsync(HttpContext, addressId);
                if (result)
                {
                    return Ok(new { Message = "Address deleted successfully." });
                }
                return BadRequest(new { Message = "Failed to delete address." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred.", Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách tất cả địa chỉ (Address) của user hiện tại
        /// </summary>
        /// <returns>Danh sách các địa chỉ dạng <see cref="AddressResponse"/> của user</returns>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (token).  
        /// 
        /// **Response codes**:
        /// - **200**: Tìm thấy danh sách địa chỉ (có thể rỗng nếu user chưa thêm địa chỉ nào)
        /// - **401**: Chưa đăng nhập/token không hợp lệ
        /// - **404**: Không có địa chỉ nào cho user này
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpGet("user")]
        public async Task<IActionResult> GetUserAddresses()
        {
            try
            {
                var addresses = await _addressService.GetUserAddressesAsync(HttpContext);
                return Ok(addresses);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred.", Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông tin địa chỉ (Address) theo <c>addressId</c> (thuộc sở hữu của user hiện tại)
        /// </summary>
        /// <param name="addressId">Id của địa chỉ cần lấy</param>
        /// <returns>Trả về đối tượng <see cref="AddressResponse"/> nếu tồn tại</returns>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (token).  
        /// **Chỉ trả về địa chỉ nếu <c>UserId</c> trùng với địa chỉ đó**.  
        /// 
        /// **Response codes**:
        /// - **200**: Tìm thấy địa chỉ
        /// - **401**: Chưa đăng nhập/token không hợp lệ
        /// - **404**: Không tìm thấy địa chỉ thuộc user
        /// - **500**: Lỗi server
        /// </remarks>
        [HttpGet("{addressId}")]
        public async Task<IActionResult> GetAddressById(Guid addressId)
        {
            try
            {
                var result = await _addressService.GetAddressByIdAsync(HttpContext, addressId);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred.", Error = ex.Message });
            }
        }
    }
}
