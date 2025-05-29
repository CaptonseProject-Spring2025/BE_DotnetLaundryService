
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace LaundryService.Api.Controllers
{
    [ApiController]
    [Route("api/staff/statistics")]
    [Authorize(Roles = "Staff")]
    public class StaffStatisticsController : BaseApiController
    {
        private readonly IStaffStatisticsService _staffStatisticsService;
        private readonly IUtil _util;

        public StaffStatisticsController(IStaffStatisticsService staffStatisticsService, IUtil util)
        {
            _staffStatisticsService = staffStatisticsService;
            _util = util;
        }

        /// <summary>
        /// Lấy thống kê tổng quan cho Staff
        /// </summary>
        /// <param name="startDate">Ngày bắt đầu (tùy chọn, mặc định 30 ngày trước)</param>
        /// <param name="endDate">Ngày kết thúc (tùy chọn, mặc định hôm nay)</param>
        /// <returns>Thống kê tổng quan của Staff</returns>
        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(StaffStatisticsResponse), 200)]
        public async Task<IActionResult> GetStaffDashboardStatistics(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            try
            {
                // Sử dụng Util để chuyển đổi từ VN time sang UTC
                var utcStartDate = startDate.HasValue
                    ? _util.ConvertVnDateTimeToUtc(startDate.Value)
                    : (DateTime?)null;
                var utcEndDate = endDate.HasValue
                    ? _util.ConvertVnDateTimeToUtc(endDate.Value)
                    : (DateTime?)null;

                var stats = await _staffStatisticsService.GetStaffStatisticsAsync(HttpContext, utcStartDate, utcEndDate);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thống kê quy trình làm việc (workflow)
        /// </summary>
        /// <returns>Thống kê workflow của Staff</returns>
        [HttpGet("workflow")]
        [ProducesResponseType(typeof(WorkflowStatistics), 200)]
        public async Task<IActionResult> GetWorkflowStatistics()
        {
            try
            {
                var stats = await _staffStatisticsService.GetWorkflowStatisticsAsync(HttpContext);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thống kê hiệu suất làm việc
        /// </summary>
        /// <returns>Thống kê hiệu suất của Staff</returns>
        [HttpGet("performance")]
        [ProducesResponseType(typeof(PerformanceStatistics), 200)]
        public async Task<IActionResult> GetPerformanceStatistics()
        {
            try
            {
                var stats = await _staffStatisticsService.GetPerformanceStatisticsAsync(HttpContext);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thống kê theo ngày
        /// </summary>
        /// <param name="date">Ngày cần thống kê</param>
        /// <returns>Thống kê trong ngày</returns>
        [HttpGet("daily")]
        [ProducesResponseType(typeof(List<StaffDailyStatistics>), 200)]
        public async Task<IActionResult> GetDailyStatistics([FromQuery] DateTime date)
        {
            try
            {
                 var utcDate = _util.ConvertVnDateTimeToUtc(date);
                var stats = await _staffStatisticsService.GetDailyStatisticsAsync(HttpContext, utcDate);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thống kê theo tuần
        /// </summary>
        /// <param name="weekStart">Ngày bắt đầu tuần</param>
        /// <returns>Thống kê theo từng ngày trong tuần</returns>

        [HttpGet("weekly")]
        [ProducesResponseType(typeof(List<StaffDailyStatistics>), 200)]
        public async Task<IActionResult> GetWeeklyStatistics([FromQuery] DateTime weekStart)
        {
            try
            {
                var utcWeekStart = _util.ConvertVnDateTimeToUtc(weekStart);
                var stats = await _staffStatisticsService.GetWeeklyStatisticsAsync(HttpContext, utcWeekStart);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thống kê theo tháng
        /// </summary>
        /// <param name="year">Năm</param>
        /// <param name="month">Tháng</param>
        /// <returns>Thống kê theo từng ngày trong tháng</returns>
        [HttpGet("monthly")]
        [ProducesResponseType(typeof(List<StaffDailyStatistics>), 200)]
         public async Task<IActionResult> GetMonthlyStatistics([FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                // Validate input parameters
                if (year < 1900 || year > 2100)
                {
                    return BadRequest(new { Message = "Year must be between 1900 and 2100" });
                }
                
                if (month < 1 || month > 12)
                {
                    return BadRequest(new { Message = "Month must be between 1 and 12" });
                }

                // Tạo DateTime UTC cho đầu tháng để đảm bảo service xử lý đúng
                var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                var stats = await _staffStatisticsService.GetMonthlyStatisticsAsync(HttpContext, year, month);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thống kê khối lượng công việc hiện tại
        /// </summary>
        /// <returns>Thống kê workload của Staff</returns>
        // [HttpGet("workload")]
        // [ProducesResponseType(typeof(StaffWorkloadStatistics), 200)]
        // public async Task<IActionResult> GetWorkloadStatistics()
        // {
        //     try
        //     {
        //         var stats = await _staffStatisticsService.GetWorkloadStatisticsAsync(HttpContext);
        //         return Ok(stats);
        //     }
        //     catch (Exception ex)
        //     {
        //         return StatusCode(500, new { Message = ex.Message });
        //     }
        // }

        /// <summary>
        /// Lấy thống kê nhanh cho dashboard
        /// </summary>
        /// <returns>Thống kê tóm tắt</returns>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummaryStatistics()
        {
            try
            {
                var workflow = await _staffStatisticsService.GetWorkflowStatisticsAsync(HttpContext);
                var workload = await _staffStatisticsService.GetWorkloadStatisticsAsync(HttpContext);
                var performance = await _staffStatisticsService.GetPerformanceStatisticsAsync(HttpContext);

                var summary = new
                {
                    CurrentWorkload = new
                    {
                        CheckingOrders = workflow.OrdersCurrentlyChecking,
                        WashingOrders = workflow.OrdersCurrentlyWashing,
                        EmergencyOrders = workload.EmergencyOrders,
                        OverdueOrders = workload.OverdueOrders
                    },
                    TodayProgress = new
                    {
                        Checked = workflow.OrdersCheckedToday,
                        Washed = workflow.OrdersWashedToday,
                        QualityChecked = workflow.OrdersQualityCheckedToday,
                        PhotosUploaded = performance.PhotosUploadedToday
                    },
                    Performance = new
                    {
                        ProductivityScore = performance.ProductivityScore,
                        AverageCheckingTime = performance.AverageCheckingTimeHours,
                        AverageWashingTime = performance.AverageWashingTimeHours
                    },
                    NextDelivery = workload.NextDeliveryTime
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }
    }
}