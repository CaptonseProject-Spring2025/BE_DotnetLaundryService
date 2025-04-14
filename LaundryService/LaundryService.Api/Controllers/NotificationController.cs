using LaundryService.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LaundryService.Api.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Lấy danh sách thông báo theo UserId
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Danh sách thông báo</returns>
        [Authorize]
        [HttpGet("userId")]
        public async Task<IActionResult> GetNotificationsByUserId()
        {
            try
            {
                var notifications = await _notificationService.GetNotificationsByUserIdAsync(HttpContext);
                return Ok(notifications);
            }
            catch (KeyNotFoundException ex) // Nếu user hợp lệ nhưng không có thông báo nào
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }


        /// <summary>
        /// Xóa một thông báo theo ID
        /// </summary>
        /// <param name="notificationId">ID của thông báo</param>
        /// <returns>Trạng thái xóa</returns>
        [HttpDelete("{notificationId}")]
        public async Task<IActionResult> Delete(Guid notificationId)
        {
            try
            {
                await _notificationService.DeleteNotificationAsync(notificationId);
                return Ok(new { Message = "Notification deleted successfully." });
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
        /// Đánh dấu thông báo là đã đọc
        /// </summary>
        /// <param name="notificationId">ID của thông báo</param>
        /// <returns>Trạng thái cập nhật</returns>
        [Authorize]
        [HttpPut("{notificationId}/read")]
        public async Task<IActionResult> MarkAsRead(Guid notificationId)
        {
            try
            {
                await _notificationService.MarkAsReadAsync(notificationId);
                return Ok(new { Message = "Notification marked as read." });
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
        /// Đánh dấu tất cả thông báo của người dùng hiện tại là đã đọc.
        /// </summary>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập (JWT).
        /// 
        /// **Logic**:
        /// - Lấy `userId` từ JWT.
        /// - Cập nhật tất cả các notification của user này có trạng thái chưa đọc thành đã đọc.
        /// </remarks>
        /// <returns>Thông báo trạng thái cập nhật</returns>
        [Authorize]
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                await _notificationService.MarkAllUserNotificationsAsReadAsync(HttpContext);
                return Ok(new { Message = "All notifications marked as read." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"An unexpected error occurred: {ex.Message}" });
            }
        }

        /// <summary>
        /// Xóa tất cả thông báo của người dùng hiện tại (dựa vào JWT).
        /// </summary>
        /// <remarks>
        /// **Yêu cầu**: Đã đăng nhập.  
        /// 
        /// **Logic**:
        /// - Lấy `userId` từ JWT.  
        /// - Xóa toàn bộ thông báo thuộc user này.
        /// 
        /// **Response codes**:
        /// - 200: Xóa thành công  
        /// - 500: Lỗi hệ thống
        /// </remarks>
        /// <returns>Thông báo trạng thái xóa</returns>
        [Authorize]
        [HttpDelete("clear-all")]
        public async Task<IActionResult> DeleteAllNotifications()
        {
            try
            {
                await _notificationService.DeleteAllNotificationsOfCurrentUserAsync(HttpContext);
                return Ok(new { Message = "Đã xóa tất cả thông báo của bạn." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Lỗi khi xóa thông báo: {ex.Message}" });
            }
        }
    }
}
