using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    /// <summary>
    /// Controller quản lý các API liên quan đến đánh giá (Rating).
    /// </summary>
    [Route("api/ratings")]
    [ApiController]
    public class RatingController : ControllerBase
    {
        private readonly IRatingService _ratingService;

        public RatingController(IRatingService ratingService)
        {
            _ratingService = ratingService;
        }

        /// <summary>
        /// Tạo đánh giá cho một đơn hàng (Customer).
        /// </summary>
        /// <param name="orderId">ID của đơn hàng cần đánh giá</param>
        /// <param name="request">Dữ liệu đánh giá (sao và nội dung)</param>
        /// <returns>200 OK với { Message = "Đánh giá thành công." } hoặc lỗi phù hợp</returns>
        [HttpPost("{orderId}/rating")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> RateOrder(string orderId, [FromBody] CreateRatingRequest request)
        {
            try
            {
                await _ratingService.CreateRatingAsync(HttpContext, orderId, request.Star, request.Review);
                return Ok(new { Message = "Đánh giá thành công." });
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
                return StatusCode(500, new { Message = "Đã có lỗi xảy ra, vui lòng thử lại sau.", Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy toàn bộ đánh giá (Admin).
        /// </summary>
        /// <returns>200 OK cùng danh sách <see cref="AdminRatingResponse"/></returns>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<AdminRatingResponse>>> GetAllRatings()
        {
            try
            {
                var result = await _ratingService.GetAllRatingsForAdminAsync(HttpContext);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Đã có lỗi xảy ra, vui lòng thử lại sau.", Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy tổng số review và điểm trung bình sao.(Admin).
        /// </summary>
        /// <returns>200 OK với { AverageStar = double }</returns>
        [HttpGet("statistics")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAverageStar()
        {
            try
            {
                var summary = await _ratingService.GetAverageStarAsync(HttpContext);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Đã có lỗi xảy ra.", Error = ex.Message });
            }
        }

        /// <summary>
        /// Thống kê đánh giá theo ngày (Admin).
        /// </summary>
        /// <param name="date">Ngày (yyyy-MM-dd)</param>
        /// <returns><see cref="RatingStatisticsResponse"/></returns>
        [HttpGet("statistics/daily")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Daily([FromQuery] DateTime date)
        {
            try
            {
                date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
                var stats = await _ratingService.GetDailyStatisticsAsync(HttpContext, date);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Đã có lỗi xảy ra, vui lòng thử lại sau.", Error = ex.Message });
            }
        }

        /// <summary>
        /// Thống kê đánh giá theo tuần (Admin).
        /// </summary>
        /// <param name="dateInWeek">Một ngày trong tuần (yyyy-MM-dd)</param>
        /// <returns><see cref="RatingStatisticsResponse"/></returns>
        [HttpGet("statistics/weekly")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Weekly([FromQuery] DateTime dateInWeek)
        {
            try
            {
                dateInWeek = DateTime.SpecifyKind(dateInWeek, DateTimeKind.Utc);
                var stats = await _ratingService.GetWeeklyStatisticsAsync(HttpContext, dateInWeek);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Đã có lỗi xảy ra, vui lòng thử lại sau.", Error = ex.Message });
            }
        }

        /// <summary>
        /// Thống kê đánh giá theo tháng (Admin).
        /// </summary>
        /// <param name="year">Năm</param>
        /// <param name="month">Tháng (1–12)</param>
        /// <returns><see cref="RatingStatisticsResponse"/></returns>
        [HttpGet("statistics/monthly")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Monthly([FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                var stats = await _ratingService.GetMonthlyStatisticsAsync(HttpContext, year, month);
                return Ok(stats);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch
            {
                return StatusCode(500, "Đã có lỗi xảy ra, vui lòng thử lại sau.");
            }
        }


        /// <summary>
        /// Lấy danh sách đánh giá trong ngày (Admin).
        /// </summary>
        /// <param name="date">Ngày cần xem (yyyy-MM-dd)</param>
        /// <returns>List&lt;AdminRatingResponse&gt;</returns>
        [HttpGet("list/daily")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ListByDay([FromQuery] DateTime date)
        {
            try
            {
                date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
                var list = await _ratingService.GetRatingsByDateAsync(HttpContext, date);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Đã có lỗi xảy ra, vui lòng thử lại sau.", Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách đánh giá trong tuần (Admin).
        /// </summary>
        /// <param name="dateInWeek">Một ngày trong tuần (yyyy-MM-dd)</param>
        /// <returns>List&lt;AdminRatingResponse&gt;</returns>
        [HttpGet("list/weekly")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ListByWeek([FromQuery] DateTime dateInWeek)
        {
            try
            {
                dateInWeek = DateTime.SpecifyKind(dateInWeek, DateTimeKind.Utc);
                var list = await _ratingService.GetRatingsByWeekAsync(HttpContext, dateInWeek);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Đã có lỗi xảy ra, vui lòng thử lại sau.", Error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách đánh giá trong tháng (Admin).
        /// </summary>
        /// <param name="year">Năm</param>
        /// <param name="month">Tháng (1–12)</param>
        /// <returns>List&lt;AdminRatingResponse&gt;</returns>
        [HttpGet("list/monthly")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ListByMonth([FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                var list = await _ratingService.GetRatingsByMonthAsync(HttpContext, year, month);
                return Ok(list);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(500, "Đã có lỗi xảy ra, vui lòng thử lại sau.");
            }
        }
    }
}
