using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IRatingService
    {
        Task CreateRatingAsync(HttpContext httpContext, string orderId, int? starRating, string? review);
        Task<List<AdminRatingResponse>> GetAllRatingsForAdminAsync(HttpContext httpContext);
        Task<StarSummaryResponse> GetAverageStarAsync(HttpContext httpContext);
        Task<RatingStatisticsResponse> GetDailyStatisticsAsync(HttpContext httpContext, DateTime date);
        Task<RatingStatisticsResponse> GetWeeklyStatisticsAsync(HttpContext httpContext, DateTime dateInWeek);
        Task<RatingStatisticsResponse> GetMonthlyStatisticsAsync(HttpContext httpContext, int year, int month);
        Task<List<AdminRatingResponse>> GetRatingsByDateAsync(HttpContext httpContext, DateTime date);
        Task<List<AdminRatingResponse>> GetRatingsByWeekAsync(HttpContext httpContext, DateTime dateInWeek);
        Task<List<AdminRatingResponse>> GetRatingsByMonthAsync(HttpContext httpContext, int year, int month);

    }
}
