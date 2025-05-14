using LaundryService.Api.Hub;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace LaundryService.Api.Controllers
{
    [Route("api/complaints")]
    [ApiController]
    [Authorize]
    public class ComplaintController : ControllerBase
    {
        private readonly IComplaintService _complaintService;
        private readonly IHubContext<ComplaintHub> _hubContext;

        public ComplaintController(IComplaintService complaintService, IHubContext<ComplaintHub> hubContext)
        {
            _complaintService = complaintService;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Tạo khiếu nại cho đơn hàng của Customer
        /// </summary>
        /// <param name="orderId">ID đơn hàng cần khiếu nại</param>
        /// <param name="request">Thông tin khiếu nại</param>
        /// <returns>Trả về thông báo thành công nếu tạo khiếu nại thành công</returns>
        [HttpPost("{orderId}/customer")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CreateComplaintForCustomer(string orderId, [FromBody] CreateComplaintRequest request)
        {
            try
            {
                await _complaintService.CreateComplaintAsyncForCustomer(HttpContext, orderId, request.ComplaintDescription, request.ComplaintType);

                await _hubContext.Clients.All.SendAsync(
                   "ReceiveComplaintNotication",
                   $"Đã có khiếu nại mới cho đơn hàng {orderId}");

                var pendingComplaints = await _complaintService.GetPendingComplaintsAsync(HttpContext);
                await _hubContext.Clients.All.SendAsync(
               "ReceiveComplaintUpdate",
               pendingComplaints);


                return Ok(new { Message = "Tạo khiếu nại thành công." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Có lỗi xảy ra khi tạo khiếu nại", Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách các đơn khiếu nại của người dùng
        /// </summary>
        /// <returns>Danh sách khiếu nại của người dùng</returns>
        /// <remarks>
        /// **User** chỉ có thể lấy các khiếu nại của chính họ.
        /// </remarks>
        [HttpGet("my-complaints")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMyComplaints()
        {
            try
            {
                var complaints = await _complaintService.GetComplaintsForCustomerAsync(HttpContext);

                return Ok(complaints);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Có lỗi xảy ra khi truy xuất danh sách khiếu nại", Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy chi tiết khiếu nại của người dùng
        /// </summary>
        /// <param name="complaintId">ID của khiếu nại cần xem chi tiết</param>
        /// <returns>Thông tin chi tiết khiếu nại</returns>
        /// <remarks>
        /// **Customer** chỉ có thể xem chi tiết khiếu nại của chính họ.
        /// </remarks>
        [HttpGet("my-complaints/{complaintId}")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetComplaintDetailForUser(Guid complaintId)
        {
            try
            {
                var complaintDetail = await _complaintService.GetComplaintDetailForCustomerAsync(HttpContext, complaintId);

                return Ok(complaintDetail);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Có lỗi xảy ra khi truy xuất chi tiết khiếu nại", Error = ex.Message });
            }
        }

        /// <summary>
        /// Customer hủy khiếu nại (chỉ khi ở trạng thái PENDING)
        /// </summary>
        [HttpPut("complaints/{complaintId}/cancel")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CancelComplaintForCustomer(Guid complaintId)
        {
            try
            {
                await _complaintService.CancelComplaintAsyncForCustomer(HttpContext, complaintId);
                return Ok(new { Message = "Hủy khiếu nại thành công." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Có lỗi xảy ra khi hủy khiếu nại", Error = ex.Message });
            }
        }

        /// <summary>
        /// Tạo khiếu nại cho đơn hàng (Admin hoặc CustomerStaff)
        /// </summary>
        /// <param name="orderId">ID đơn hàng cần khiếu nại</param>
        /// <param name="request">Thông tin khiếu nại</param>
        /// <returns>Trả về thông báo thành công nếu tạo khiếu nại thành công</returns>
        [HttpPost("{orderId}/admin-customerstaff")]
        [Authorize(Roles = "Admin,CustomerStaff")]
        public async Task<IActionResult> CreateComplaintForAdminOrStaff(string orderId, [FromBody] CreateComplaintRequest request)
        {
            try
            {
                await _complaintService.CreateComplaintAsyncForAdminOrCustomerStaff(HttpContext, orderId, request.ComplaintDescription, request.ComplaintType);

                await _hubContext.Clients.All.SendAsync(
                    "ReceiveComplaintNotication",
                    $"Đã có khiếu nại mới cho đơn hàng {orderId}");

                var pendingComplaints = await _complaintService.GetPendingComplaintsAsync(HttpContext);
                await _hubContext.Clients.All.SendAsync(
               "ReceiveComplaintUpdate",
               pendingComplaints);

                return Ok(new { Message = "Tạo khiếu nại thành công." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Có lỗi xảy ra khi tạo khiếu nại", Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách khiếu nại ở trạng thái Pending (Admin hoặc CustomerStaff)
        /// </summary>
        /// <returns>Danh sách các khiếu nại ở trạng thái Pending</returns>
        [HttpGet("pending")]
        [Authorize(Roles = "Admin,CustomerStaff")]
        public async Task<IActionResult> GetPendingComplaints()
        {
            try
            {
                var pendingComplaints = await _complaintService.GetPendingComplaintsAsync(HttpContext);
                await _hubContext.Clients.All.SendAsync(
                   "ReceiveComplaintUpdate",
                   pendingComplaints);
                return Ok(pendingComplaints);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Có lỗi xảy ra khi truy xuất khiếu nại", Error = ex.Message });
            }
        }

        /// <summary>
        /// Xem chi tiết khiếu nại theo ComplaintId (Admin hoặc CustomerStaff)
        /// </summary>
        /// <param name="complaintId">ID của khiếu nại</param>
        /// <returns>Trả về thông tin chi tiết của khiếu nại</returns>
        [HttpGet("{complaintId}/detail")]
        [Authorize(Roles = "Admin,CustomerStaff")]
        public async Task<IActionResult> GetComplaintDetail(Guid complaintId)
        {
            try
            {
                var complaintDetail = await _complaintService.GetComplaintDetailAsync(HttpContext, complaintId);

                return Ok(complaintDetail);
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
                return StatusCode(500, new { Message = "Có lỗi xảy ra khi truy xuất chi tiết khiếu nại", Error = ex.Message });
            }
        }

        /// <summary>
        /// Nhận xử lý khiếu nại cho đơn hàng (Admin hoặc CustomerStaff)
        /// </summary>
        /// <param name="complaintId">ID của khiếu nại</param>
        /// <returns>Trả về thông báo thành công nếu nhận xử lý thành công</returns>
        [HttpPost("{complaintId}/accept")]
        [Authorize(Roles = "Admin,CustomerStaff")]
        public async Task<IActionResult> AcceptComplaintForProcessing(Guid complaintId)
        {
            try
            {
                await _complaintService.AcceptComplaintAsync(HttpContext, complaintId);

                await _hubContext.Clients.All.SendAsync(
                    "ReceiveComplaintUpdate",
                    $"Khiếu nại {complaintId} đã được nhận xử lý và có trạng thái IN_PROGRESS.");

                return Ok(new { Message = "Khiếu nại đã được nhận xử lý thành công." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Có lỗi xảy ra khi nhận xử lý khiếu nại", Error = ex.Message });
            }
        }

        /// <summary>
        /// Hoàn thành khiếu nại (Admin hoặc CustomerStaff)
        /// </summary>
        /// <param name="complaintId">ID của khiếu nại</param>
        /// <param name="resolutionDetails">Thông tin chi tiết giải quyết</param>
        /// <returns>Trả về thông báo thành công nếu hoàn thành khiếu nại thành công</returns>
        [HttpPost("{complaintId}/complete")]
        [Authorize(Roles = "Admin,CustomerStaff")]
        public async Task<IActionResult> CompleteComplaint(Guid complaintId, [FromBody] string resolutionDetails)
        {
            try
            {
                await _complaintService.CompleteComplaintAsync(HttpContext, complaintId, resolutionDetails);

                await _hubContext.Clients.All.SendAsync(
                    "ReceiveComplaintUpdate",
                    $"Khiếu nại {complaintId} đã hoàn thành và có trạng thái RESOLVED.");

                return Ok(new { Message = "Khiếu nại đã được hoàn thành." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Có lỗi xảy ra khi hoàn thành khiếu nại", Error = ex.Message });
            }
        }


        /// <summary>
        /// Lấy danh sách khiếu nại có trạng thái IN_PROGRESS(Admin hoặc CustomerStaff)
        /// </summary>
        /// <returns>Danh sách các khiếu nại có trạng thái IN_PROGRESS</returns>
        /// <remarks>
        /// **Admin** có thể xem tất cả các khiếu nại đã được nhận xử lý.  
        /// **CustomerStaff** chỉ có thể xem khiếu nại mà họ đã xử lý.
        /// </remarks>
        [HttpGet("in-progress")]
        [Authorize(Roles = "Admin,CustomerStaff")]
        public async Task<IActionResult> GetInProgressComplaints()
        {
            try
            {
                if (User.IsInRole("Admin"))
                {
                    var complaints = await _complaintService.GetInProgressComplaintsForAdminAsync(HttpContext);
                    return Ok(complaints);
                }
                else if (User.IsInRole("CustomerStaff"))
                {
                    var complaints = await _complaintService.GetInProgressComplaintsForCustomerStaffAsync(HttpContext);
                    return Ok(complaints);
                }
                else
                {
                    return Unauthorized(new { Message = "Bạn không có quyền truy cập dữ liệu này." });
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Có lỗi xảy ra khi truy xuất danh sách khiếu nại", Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách đơn khiếu nại có trạng thái RESOLVED(Admin hoặc CustomerStaff)
        /// </summary>
        /// <returns>Danh sách các khiếu nại đã được hoàn thành</returns>
        /// <remarks>
        /// **Admin** có thể xem tất cả các khiếu nại đã được hoàn thành.  
        /// **CustomerStaff** chỉ có thể xem khiếu nại mà họ đã xử lý và có trạng thái RESOLVED.
        /// </remarks>
        [HttpGet("resolved")]
        [Authorize(Roles = "Admin,CustomerStaff")]
        public async Task<IActionResult> GetResolvedComplaints()
        {
            try
            {
                if (User.IsInRole("Admin"))
                {
                    var complaints = await _complaintService.GetResolvedComplaintsForAdminAsync(HttpContext);
                    return Ok(complaints);
                }
                else if (User.IsInRole("CustomerStaff"))
                {
                    var complaints = await _complaintService.GetResolvedComplaintsForCustomerStaffAsync(HttpContext);
                    return Ok(complaints);
                }
                else
                {
                    return Unauthorized(new { Message = "Bạn không có quyền truy cập dữ liệu này." });
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Có lỗi xảy ra khi truy xuất danh sách khiếu nại", Error = ex.Message });
            }
        }
    }
}
