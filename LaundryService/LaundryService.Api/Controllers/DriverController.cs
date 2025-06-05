using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using LaundryService.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [ApiController]
    [Route("api/driver")]
    [Authorize(Roles = "Driver")]
    public class DriverController : BaseApiController
    {
        private readonly IDriverService _driverService;
        private readonly IOrderService _orderService;
        private readonly IAddressService _addressService;
        private readonly IOrderAssignmentHistoryService _orderAssignmentHistoryService;
        private readonly IFirebaseNotificationService _firebaseNotificationService;
        private readonly INotificationService _notificationService;

        public DriverController(IDriverService driverService, IOrderService orderService, IAddressService addressService, IOrderAssignmentHistoryService orderAssignmentHistoryService, IFirebaseNotificationService firebaseNotificationService, INotificationService notificationService)
        {
            _driverService = driverService;
            _orderService = orderService;
            _addressService = addressService;
            _orderAssignmentHistoryService = orderAssignmentHistoryService;
            _firebaseNotificationService = firebaseNotificationService;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Tài xế bắt đầu nhận hàng
        /// </summary>
        [Authorize]
        [HttpPost("start-pickup")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> StartOrderPickup([FromQuery] string orderId)
        {
            try
            {
                await _driverService.StartOrderPickupAsync(HttpContext, orderId);

                var customerId = await _orderService.GetCustomerIdByOrderAsync(orderId);

                try
                {
                    await _notificationService.CreatePickupStartedNotificationAsync(customerId, orderId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi tạo notification trong hệ thống: {ex.Message}");
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _firebaseNotificationService.SendOrderNotificationAsync(
                            customerId.ToString(),
                            NotificationType.PickupStarted,
                            new Dictionary<string, string> { ["orderId"] = orderId }
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi gửi thông báo: {ex.Message}");
                    }
                });

                return Ok(new { Message = "Tài xế đã bắt đầu đi nhận hàng (PICKING_UP)." });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                // Log ra logger nào đó
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Xác nhận đã nhận hàng (PICKED_UP)
        /// </summary>
        [HttpPost("confirm-picked-up")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> ConfirmOrderPickedUp(
        [FromForm] string orderId,
        [FromForm] string notes,
        [FromForm] List<IFormFile> files)
        {
            try
            {
                await _driverService.ConfirmOrderPickedUpAsync(HttpContext, orderId, notes);

                var customerId = await _orderService.GetCustomerIdByOrderAsync(orderId);

                try
                {
                    await _notificationService.CreateOrderPickedUpNotificationAsync(customerId, orderId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi tạo notification trong hệ thống: {ex.Message}");
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _firebaseNotificationService.SendOrderNotificationAsync(
                            customerId.ToString(),
                            NotificationType.PickedUp,
                            new Dictionary<string, string> { ["orderId"] = orderId }
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi gửi thông báo: {ex.Message}");
                    }
                });

                return Ok(new { Message = "Tài xế đã xác nhận nhận hàng thành công (PICKED_UP)." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }


        /// <summary>
        /// Xác nhận tài xế đã mang hàng về (ARRIVED) kèm ảnh chứng minh
        /// </summary>
        [HttpPost("confirm-pickup-arrived")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> ConfirmOrderPickupArrived(
    [FromForm] string orderId,
    [FromForm] List<IFormFile> files)
        {
            try
            {
                await _driverService.ConfirmOrderPickupArrivedAsync(HttpContext, orderId);

                var customerId = await _orderService.GetCustomerIdByOrderAsync(orderId);

                try
                {
                    await _notificationService.CreatePickupArrivedNotificationAsync(customerId, orderId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi khi tạo notification: {ex.Message}");
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _firebaseNotificationService.SendOrderNotificationAsync(
                            customerId.ToString(),
                            NotificationType.PickupArrived,
                            new Dictionary<string, string> { ["orderId"] = orderId }
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi gửi Firebase push: {ex.Message}");
                    }
                });

                return Ok(new { Message = "Tài xế đã mang hàng về thành công (ARRIVED)." });
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
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }



        /// <summary>
        /// Huỷ nhận hàng
        /// </summary>
        [HttpPost("cancel-pickup")]
        public async Task<IActionResult> CancelAssignedPickup([FromBody] CancelPickupRequest request)
        {
            try
            {
                await _driverService.CancelAssignedPickupAsync(
                    HttpContext,
                    request.OrderId,
                    request.CancelReason);

                return Ok(new { Message = "Huỷ nhận hàng thành công." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
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
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Huỷ nhận hàng vì khách không có mặt (no-show).
        /// </summary>
        [HttpPost("{orderId}/pickup/cancel/noshow")]
        public async Task<IActionResult> CancelPickupNoShowAsync(string orderId)
        {
            try
            {
                await _driverService.CancelPickupNoShowAsync(HttpContext, orderId);
                return Ok(new { message = "Huỷ nhận hàng (khách không có mặt) thành công." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Tài xế bắt đầu giao hàng
        /// </summary>
        [HttpPost("start-delivery")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> StartOrderDelivery([FromQuery] string orderId)
        {
            try
            {
                await _driverService.StartOrderDeliveryAsync(HttpContext, orderId);

                var customerId = await _orderService.GetCustomerIdByOrderAsync(orderId);

                try
                {
                    await _notificationService.CreateDeliveryStartedNotificationAsync(customerId, orderId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi tạo notification trong hệ thống: {ex.Message}");
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _firebaseNotificationService.SendOrderNotificationAsync(
                            customerId.ToString(),
                            NotificationType.DeliveryStarted,
                            new Dictionary<string, string> { ["orderId"] = orderId }
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi gửi thông báo: {ex.Message}");
                    }
                });

                return Ok(new { Message = "Tài xế đã bắt đầu đi giao hàng (DELIVERING)." });
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
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Xác nhận đã giao hàng (DELIVERED)
        /// </summary>
        [HttpPost("confirm-delivered")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> ConfirmOrderDelivered(
            [FromForm] string orderId,
            [FromForm] string notes,
            [FromForm] List<IFormFile> files)
        {
            try
            {
                await _driverService.ConfirmOrderDeliveredAsync(HttpContext, orderId, notes);

                var customerId = await _orderService.GetCustomerIdByOrderAsync(orderId);

                try
                {
                    await _notificationService.CreateOrderDeliveredNotificationAsync(customerId, orderId);
                    await _notificationService.CreateThankYouNotificationAsync(customerId, orderId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi tạo notification trong hệ thống: {ex.Message}");
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _firebaseNotificationService.SendOrderNotificationAsync(
                            customerId.ToString(),
                            NotificationType.Delivered,
                            new Dictionary<string, string> { ["orderId"] = orderId }
                        );

                        await _firebaseNotificationService.SendOrderNotificationAsync(
                            customerId.ToString(),
                            NotificationType.Finish
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi gửi thông báo: {ex.Message}");
                    }
                });

                return Ok(new { Message = "Tài xế đã xác nhận giao hàng thành công (DELIVERED)." });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
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
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Xác nhận hoàn tất giao hàng
        /// </summary>
        [HttpPost("confirm-delivery-success")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> ConfirmFinishDelivery([FromQuery] string orderId)
        {
            try
            {
                await _driverService.ConfirmOrderDeliverySuccessAsync(HttpContext, orderId);

                return Ok(new { Message = "Tài xế đã xác nhận giao hàng thành công và đã về." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Huỷ giao hàng, lý do hủy từ tài xế
        /// </summary>
        [HttpPost("cancel-delivery")]
        public async Task<IActionResult> CancelDelivery([FromBody] CancelDeliveryRequest request)
        {
            try
            {
                await _driverService.CancelAssignedDeliveryAsync(
                    HttpContext,
                    request.OrderId,
                    request.CancelReason);

                return Ok(new { Message = "Huỷ giao hàng thành công." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
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
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy địa chỉ nơi nhận hàng
        /// </summary>
        [HttpGet("pickup-address")]
        [ProducesResponseType(typeof(AddressInfoResponse), 200)]
        public async Task<IActionResult> GetPickupAddress([FromQuery] Guid assignmentId)
        {
            try
            {
                var result = await _addressService.GetPickupAddressFromAssignmentAsync(HttpContext, assignmentId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy địa chỉ nơi giao hàng
        /// </summary>
        [HttpGet("delivery-address")]
        [ProducesResponseType(typeof(AddressInfoResponse), 200)]
        public async Task<IActionResult> GetDeliveryAddress([FromQuery] Guid assignmentId)
        {
            try
            {
                var result = await _addressService.GetDeliveryAddressFromAssignmentAsync(HttpContext, assignmentId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy danh sách các nhiệm vụ tài xế được phân công.
        /// </summary>
        [HttpGet("my-assignments")]
        [ProducesResponseType(typeof(List<AssignmentHistoryResponse>), 200)]
        public async Task<IActionResult> GetMyAssignments()
        {
            try
            {
                var result = await _orderAssignmentHistoryService.GetAssignmentsForDriverAsync(HttpContext);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy chi tiết nhiệm vụ
        /// </summary>
        [HttpGet("assignments/{assignmentId}")]
        [ProducesResponseType(typeof(AssignmentDetailResponse), 200)]
        public async Task<IActionResult> GetAssignmentDetail(Guid assignmentId)
        {
            try
            {
                var result = await _orderAssignmentHistoryService.GetAssignmentDetailAsync(HttpContext, assignmentId);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return NotFound(new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Huỷ giao hàng vì khách không có mặt (no-show).
        /// </summary>
        [HttpPost("{orderId}/delivery/cancel/noshow")]
        public async Task<IActionResult> CancelDeliveryNoShowAsync(string orderId)
        {
            try
            {
                await _driverService.CancelDeliveryNoShowAsync(HttpContext, orderId);
                return Ok(new { message = "Huỷ giao hàng (khách không có mặt) thành công." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thống kê tổng hợp của tài xế cho một ngày nhất định.
        /// </summary>
        [HttpGet("statistics/daily")]
        public async Task<IActionResult> GetDailyStatistics([FromQuery] DateTime date)
        {
            try
            {
                var result = await _driverService.GetDailyStatisticsAsync(HttpContext, date);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy thống kê tổng hợp của tài xế cho một tuần nhất định.
        /// </summary>
        [HttpGet("statistics/weekly")]
        public async Task<IActionResult> GetWeeklyStatistics([FromQuery] DateTime dateInWeek)
        {
            try
            {
                var result = await _driverService.GetWeeklyStatisticsAsync(HttpContext, dateInWeek);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy thống kê tổng hợp của tài xế cho một tháng nhất định.
        /// </summary>
        [HttpGet("statistics/monthly")]
        public async Task<IActionResult> GetMonthlyStatistics([FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                var result = await _driverService.GetMonthlyStatisticsAsync(HttpContext, year, month);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy danh sách chi tiết các đơn hàng đã hoàn thành của tài xế cho một ngày nhất định.
        /// </summary>
        [HttpGet("statistics/daily/list")]
        [ProducesResponseType(typeof(List<DriverStatisticsListResponse>), 200)]
        public async Task<IActionResult> GetDailyStatisticsList([FromQuery] DateTime date)
        {
            try
            {
                var list = await _driverService.GetDailyStatisticsListAsync(HttpContext, date);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy danh sách chi tiết các đơn hàng đã hoàn thành của tài xế cho một tuần nhất định.
        /// </summary>
        [HttpGet("statistics/weekly/list")]
        [ProducesResponseType(typeof(List<DriverStatisticsListResponse>), 200)]
        public async Task<IActionResult> GetWeeklyStatisticsList([FromQuery] DateTime dateInWeek)
        {
            try
            {
                var list = await _driverService.GetWeeklyStatisticsListAsync(HttpContext, dateInWeek);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy danh sách chi tiết các đơn hàng đã hoàn thành của tài xế cho một tháng nhất định.
        /// </summary>
        [HttpGet("statistics/monthly/list")]
        [ProducesResponseType(typeof(List<DriverStatisticsListResponse>), 200)]
        public async Task<IActionResult> GetMonthlyStatisticsList([FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                var list = await _driverService.GetMonthlyStatisticsListAsync(HttpContext, year, month);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

    }
}
