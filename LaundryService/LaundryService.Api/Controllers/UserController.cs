using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using LaundryService.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace LaundryService.Api.Controllers
{
    /// <summary>
    /// Controller quản lý Users
    /// </summary>
    [Route("api/users")]
    [ApiController]
    public class UserController : BaseApiController
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Lấy thông tin chi tiết của một User thông qua Id
        /// </summary>
        /// <param name="id">Guid (Id) của User cần lấy thông tin</param>
        /// <returns>Trả về <see cref="UserDetailResponse"/> nếu tìm thấy</returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Phải đăng nhập (có token), không phân biệt role.
        /// 
        /// **Response codes**:
        /// - **200**: Lấy thành công.
        /// - **404**: Không tìm thấy User với Id tương ứng.
        /// - **500**: Lỗi phía server.
        /// </remarks>
        [Authorize]
        [HttpGet("{id}")]
        //[ProducesResponseType(typeof(UserDetailResponse), (int)HttpStatusCode.OK)]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.NotFound)]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
        public async Task<IActionResult> GetUserById(Guid id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                return Ok(user);
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

        /// <summary>Tìm user theo số điện thoại.</summary>
        /// <param name="phone">Số điện thoại cần tìm (chính xác).</param>
        [Authorize]                                              // tuỳ bảo mật
        [HttpGet("search")]
        [ProducesResponseType(typeof(UserDetailResponse), 200)]
        public async Task<IActionResult> SearchByPhone([FromQuery] string phone)
        {
            try
            {
                var user = await _userService.GetUserByPhoneAsync(phone);
                return Ok(user);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { Message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { Message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        /// <summary>
        /// Kiểm tra xem một số điện thoại (phone) có tồn tại trên hệ thống không
        /// </summary>
        /// <param name="phone">Số điện thoại cần kiểm tra (định dạng 10 chữ số)</param>
        /// <returns>
        /// Trả về **200** kèm thông báo `"Phone number is exists"` nếu tồn tại,  
        /// Trả về **400** kèm thông báo `"Phone number is not exists"` nếu không tồn tại.
        /// </returns>
        /// <remarks>
        /// **Không yêu cầu đăng nhập** (Public).
        /// 
        /// **Response codes**:
        /// - **200**: Số điện thoại đã tồn tại trên hệ thống.
        /// - **400**: Số điện thoại chưa tồn tại.
        /// - **500**: Lỗi server.
        /// </remarks>
        [HttpPost("check-phone")]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.BadRequest)]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
        public async Task<IActionResult> CheckPhoneNumberExists(string phone)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                if (await _userService.CheckPhoneNumberExistsAsync(phone))
                {
                    return Ok(new { Message = "Phone number is exists" });
                }
                return BadRequest(new { Message = "Phone number is not exists" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred" });
            }
        }

        /// <summary>
        /// Cập nhật thông tin hồ sơ của User (FullName, Email, Dob, Gender, Avatar)
        /// </summary>
        /// <param name="request">
        /// Request update, trong đó có:
        /// - **UserId** (Guid) của user
        /// - **FullName** (tùy chọn)
        /// - **Email** (tùy chọn)
        /// - **Dob** (Ngày sinh, tùy chọn)
        /// - **Gender** (Giới tính, tùy chọn)
        /// - **Avatar** (file upload, tùy chọn)
        /// </param>
        /// <returns>Trả về thông tin user sau khi cập nhật</returns>
        /// <remarks>
        /// **Yêu cầu**: Cần đăng nhập (có token). 
        /// 
        /// **Response codes**:
        /// - **200**: Cập nhật thành công, trả về user đã cập nhật.
        /// - **404**: Không tìm thấy user với UserId trong request.
        /// - **400**: Dữ liệu gửi lên không hợp lệ, hoặc email bị trùng.
        /// - **500**: Lỗi server.
        /// </remarks>
        [Authorize]
        [HttpPut("update-profile")]
        //[ProducesResponseType(typeof(UserDetailResponse), (int)HttpStatusCode.OK)]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.NotFound)]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.BadRequest)]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
        public async Task<IActionResult> UpdateUserProfile([FromForm] UpdateUserProfileRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var updatedUser = await _userService.UpdateUserProfileAsync(request);
                return Ok(updatedUser);
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

        /// <summary>
        /// Xóa (chuyển trạng thái thành "Deleted") một User qua Id
        /// </summary>
        /// <param name="id">Guid (Id) của User cần xóa</param>
        /// <returns>Trả về thông báo xóa thành công</returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Phải đăng nhập **với role = Admin**.
        /// 
        /// **Lưu ý**: Chúng ta không xóa cứng mà chỉ chuyển trường `Status = "Deleted"`.  
        /// Đồng thời xóa RefreshToken để user không thể sử dụng token cũ.
        /// 
        /// **Response codes**:
        /// - **200**: Xóa (update status) thành công
        /// - **404**: Không tìm thấy User
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.NotFound)]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            try
            {
                await _userService.DeleteUserAsync(id);
                return Ok(new { Message = "User deleted successfully." });
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
        /// (Admin, CustomerStaff) Lấy danh sách Users với bộ lọc Role (tùy chọn) và phân trang
        /// </summary>
        /// <param name="role">Role của User (Admin/Customer/Staff). Nếu rỗng => lấy tất cả</param>
        /// <param name="page">Trang hiện tại (mặc định = 1)</param>
        /// <param name="pageSize">Số user trên mỗi trang (mặc định = 10)</param>
        /// <returns>Mảng user trong trang hiện tại, kèm thông tin phân trang</returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Phải đăng nhập với **role = Admin**.
        /// 
        /// **Response codes**:
        /// - **200**: Lấy dữ liệu thành công
        /// - **401**: Chưa đăng nhập hoặc không phải Admin
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin,CustomerStaff")]
        [HttpGet]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.Unauthorized)]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
        public async Task<IActionResult> GetUsers([FromQuery] string? role, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var users = await _userService.GetUsersAsync(HttpContext, role, page, pageSize);
                return Ok(users);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// (Admin, CustomerStaff) Tạo mới 1 user (Fullname, Email, Password, Role, Avatar, Dob, Gender, PhoneNumber, RewardPoints)
        /// </summary>
        /// <param name="request">
        /// Dữ liệu tạo user:
        /// - **FullName** (bắt buộc)
        /// - **Email** (có thể null, rỗng hoặc bỏ qua)
        /// - **Password** (bắt buộc, tối thiểu 8 ký tự, có chữ hoa, số, ký tự đặc biệt)
        /// - **Role** (bắt buộc, Admin/Customer/Staff)
        /// - **Avatar** (file upload, tùy chọn)
        /// - **Dob** (ngày sinh, tùy chọn)
        /// - **Gender** (bắt buộc: Male/Female/Other)
        /// - **PhoneNumber** (bắt buộc, 10 chữ số)
        /// - **RewardPoints** (tùy chọn, default = 0)
        /// </param>
        /// <returns>Trả về thông tin user vừa tạo</returns>
        /// <remarks>
        /// **Yêu cầu quyền**: Phải đăng nhập với **role = Admin**.  
        /// **Chú ý**: Gửi request dạng `multipart/form-data` nếu có file Avatar.
        /// 
        /// **Response codes**:
        /// - **200**: Tạo thành công, trả về user mới
        /// - **400**: Số điện thoại/Email đã tồn tại hoặc dữ liệu không hợp lệ
        /// - **500**: Lỗi server
        /// </remarks>
        [Authorize(Roles = "Admin,CustomerStaff")]
        [HttpPost("create")]
        [ProducesResponseType(typeof(UserDetailResponse), (int)HttpStatusCode.OK)]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.BadRequest)]
        //[ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
        public async Task<IActionResult> CreateUser([FromForm] CreateUserRequest request)
        {
            // Lưu ý: [FromForm] để upload file (Avatar) qua multipart/form-data
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var newUser = await _userService.CreateUserAsync(request);
                return Ok(newUser);
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

        /// <summary>
        /// Lấy danh sách lịch sử tích điểm thưởng của user
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [HttpGet("reward-points-history")]
        [ProducesResponseType(typeof(List<RewardHistoryResponse>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetRewardHistory()
        {
            try
            {
                var rewardHistory = await _userService.GetRewardHistoryAsync(HttpContext);
                return Ok(rewardHistory);
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
        /// Lấy <b>danh sách tài xế khả dụng</b> cùng số đơn đang được giao / nhận.
        /// </summary>
        /// <returns>
        /// Trả về một đối tượng ẩn danh chứa:
        /// <list type="bullet">
        ///   <item>
        ///     <description><c>count</c>: số tài xế thỏa điều kiện.</description>
        ///   </item>
        ///   <item>
        ///     <description><c>data</c>: mảng các <see cref="DriverResponse"/>.</description>
        ///   </item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// **Điều kiện tài xế được đưa vào danh sách**  
        /// - `Role == "Driver"` và `Status == "Active"`.  
        /// - Không nằm trong bất kỳ bản ghi <c>Absentdriver</c> đang hiệu lực tại thời điểm gọi (bao gồm cả bản ghi cũ lưu giờ VN).  
        /// 
        /// **Thông tin thống kê kèm theo mỗi tài xế**  
        /// - <c>PickupOrderCount</c>: tổng đơn ở trạng thái <c>ASSIGNED_PICKUP</c>.  
        /// - <c>DeliveryOrderCount</c>: tổng đơn ở trạng thái <c>ASSIGNED_DELIVERY</c>.  
        /// - <c>CurrentOrderCount</c>: tổng hai giá trị trên.  
        /// 
        /// **Yêu cầu quyền**: Phải đăng nhập với vai trò **Admin**.  
        /// 
        /// **Response codes**  
        /// - **200** – Trả về danh sách tài xế.  
        /// - **500** – Lỗi phía server.  
        /// </remarks>

        [HttpGet("drivers/available")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAvailableDrivers()
        {
            try
            {
                var drivers = await _userService.GetAvailableDriversAsync();

                return Ok(new
                {
                    Count = drivers.Count,
                    Data = drivers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

    }
}
