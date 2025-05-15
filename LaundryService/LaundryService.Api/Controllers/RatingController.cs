using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
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
        /// Đánh giá đơn hàng cho người dùng
        /// </summary>
        /// <param name="orderId">ID đơn hàng</param>
        /// <param name="request">Thông tin đánh giá</param>
        /// <returns>Trả về thông báo thành công nếu tạo đánh giá thành công</returns>
        [HttpPost("{orderId}/rate")]
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
                return StatusCode(500, new { Message = "Có lỗi xảy ra khi tạo đánh giá", Error = ex.Message });
            }
        }

        /// <summary>
        /// Admin lấy danh sách tất cả các đánh giá
        /// </summary>
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
                return StatusCode(500, new { Message = "Lỗi server", Error = ex.Message }); 
            }
        }

        /// <summary>
        /// Admin lấy trung bình số sao của tất cả đánh giá
        /// </summary>
        [HttpGet("average")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAverageStar()
        {
            try
            {
                var avg = await _ratingService.GetAverageStarAsync(HttpContext);
                return Ok(new { AverageStar = avg });
            }
            catch (UnauthorizedAccessException ex) 
            {
                return Unauthorized(new { Message = ex.Message }); 
            }
            catch (Exception ex) 
            { 
                return StatusCode(500, new { Message = "Lỗi server", Error = ex.Message }); 
            }
        }
    }

}
