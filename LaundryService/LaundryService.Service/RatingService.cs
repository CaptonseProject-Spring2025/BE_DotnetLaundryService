using LaundryService.Domain.Entities;
using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class RatingService : IRatingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUtil _util;

        public RatingService(IUnitOfWork unitOfWork, IUtil util)
        {
            _unitOfWork = unitOfWork;
            _util = util;
        }

        public async Task CreateRatingAsync(HttpContext httpContext, string orderId, int? starRating, string? review)
        {
            if (!starRating.HasValue && string.IsNullOrWhiteSpace(review))
                throw new ArgumentException("Vui lòng cung cấp sao hoặc nội dung đánh giá.");

            if (starRating.HasValue && (starRating.Value < 1 || starRating.Value > 5))
                throw new ArgumentException("Số sao phải nằm trong khoảng từ 1 đến 5.");

            var customerId = _util.GetCurrentUserIdOrThrow(httpContext);

            var order = await _unitOfWork.Repository<Order>()
                .GetAsync(o => o.Orderid == orderId);
            if (order == null)
                throw new KeyNotFoundException("Đơn hàng không tồn tại.");

            if (order.Userid != customerId)
                throw new UnauthorizedAccessException("Đơn hàng này không thuộc về bạn.");

            if (order.Currentstatus != OrderStatusEnum.DELIVERED.ToString()
             && order.Currentstatus != OrderStatusEnum.COMPLETED.ToString())
                throw new InvalidOperationException("Chỉ có thể đánh giá khi đơn đã hoàn tất.");

            var existing = await _unitOfWork.Repository<Rating>()
                .GetAsync(r => r.Orderid == orderId && r.Userid == customerId);
            if (existing != null)
                throw new InvalidOperationException("Bạn đã đánh giá đơn này rồi.");

            var rating = new Rating
            {
                Ratingid = Guid.NewGuid(),
                Userid = customerId,
                Orderid = orderId,
                Star = starRating,
                Review = string.IsNullOrWhiteSpace(review) ? null : review.Trim(),
                Createdat = DateTime.UtcNow
            };

            await _unitOfWork.Repository<Rating>().InsertAsync(rating);
            await _unitOfWork.SaveChangesAsync();
        }


        public async Task<List<AdminRatingResponse>> GetAllRatingsForAdminAsync(HttpContext httpContext)
        {

            var list = await _unitOfWork.Repository<Rating>()
                .GetAll()
                .OrderByDescending(r => r.Createdat)
                .Include(r => r.User)
                .ToListAsync();

            return list
                .Select(r => new AdminRatingResponse
                {
                    OrderId = r.Orderid,
                    FullName = r.User.Fullname,
                    Star = r.Star,
                    Review = r.Review,
                    CreatedAt = r.Createdat ?? DateTime.MinValue
                })
                .ToList();
        }

        public async Task<StarSummaryResponse> GetRatingSummaryAsync(HttpContext httpContext)
        {
            var repo = _unitOfWork.Repository<Rating>().GetAll();
            var totalRatings = await repo.CountAsync();
            var totalReviews = await repo
                .Where(r => !string.IsNullOrWhiteSpace(r.Review))
                .CountAsync();
            var starValues = repo
                .Where(r => r.Star.HasValue)
                .Select(r => r.Star.Value);
            var starCount = await starValues.CountAsync();
            var average = starCount == 0
                ? 0
                : await starValues.AverageAsync();

            return new StarSummaryResponse
            {
                TotalRatings = totalRatings,
                TotalReviews = totalReviews,
                AverageStar = average
            };
        }

        public async Task<RatingStatisticsResponse> GetDailyStatisticsAsync(HttpContext httpContext, DateTime date)
        {
            date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var start = date;
            var end = start.AddDays(1);

            var allQuery = _unitOfWork.Repository<Rating>()
                .GetAll()
                .Where(r => r.Createdat >= start && r.Createdat < end);
            var totalRatings = await allQuery.CountAsync();
            var totalReviews = await allQuery
                .Where(r => !string.IsNullOrWhiteSpace(r.Review))
                .CountAsync();
            var starValues = allQuery
                .Where(r => r.Star.HasValue)
                .Select(r => r.Star.Value);

            var starCount = await starValues.CountAsync();
            var averageStar = starCount == 0
                ? 0
                : await starValues.AverageAsync();

            return new RatingStatisticsResponse
            {
                PeriodStart = start,
                PeriodEnd = end,
                TotalRatings = totalRatings,
                TotalReviews = totalReviews,
                AverageStar = averageStar
            };
        }

        public async Task<RatingStatisticsResponse> GetWeeklyStatisticsAsync(HttpContext httpContext, DateTime dateInWeek)
        {
            dateInWeek = DateTime.SpecifyKind(dateInWeek.Date, DateTimeKind.Utc);
            var diff = (7 + (dateInWeek.DayOfWeek - DayOfWeek.Monday)) % 7;
            var start = dateInWeek.AddDays(-diff);
            var end = start.AddDays(7);

            var allQuery = _unitOfWork.Repository<Rating>()
                .GetAll()
                .Where(r => r.Createdat >= start && r.Createdat < end);

            var totalRatings = await allQuery.CountAsync();
            var totalReviews = await allQuery
                .Where(r => !string.IsNullOrWhiteSpace(r.Review))
                .CountAsync();

            var starValues = allQuery
                .Where(r => r.Star.HasValue)
                .Select(r => r.Star.Value);

            var starCount = await starValues.CountAsync();
            var averageStar = starCount == 0
                ? 0
                : await starValues.AverageAsync();

            return new RatingStatisticsResponse
            {
                PeriodStart = start,
                PeriodEnd = end,
                TotalRatings = totalRatings,
                TotalReviews = totalReviews,
                AverageStar = averageStar
            };
        }

        public async Task<RatingStatisticsResponse> GetMonthlyStatisticsAsync(HttpContext httpContext, int year, int month)
        {
            if (month < 1 || month > 12)
                throw new ArgumentException("Tháng phải nằm trong khoảng 1–12.");

            var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);

            var allQuery = _unitOfWork.Repository<Rating>()
                .GetAll()
                .Where(r => r.Createdat >= start && r.Createdat < end);

            var totalRatings = await allQuery.CountAsync();
            var totalReviews = await allQuery
                .Where(r => !string.IsNullOrWhiteSpace(r.Review))
                .CountAsync();

            var starValues = allQuery
                .Where(r => r.Star.HasValue)
                .Select(r => r.Star.Value);

            var starCount = await starValues.CountAsync();
            var averageStar = starCount == 0
                ? 0
                : await starValues.AverageAsync();

            return new RatingStatisticsResponse
            {
                PeriodStart = start,
                PeriodEnd = end,
                TotalRatings = totalRatings,
                TotalReviews = totalReviews,
                AverageStar = averageStar
            };
        }

        public async Task<List<AdminRatingResponse>> GetRatingsByDateAsync(HttpContext httpContext, DateTime date)
        {
            date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var start = date;
            var end = start.AddDays(1);

            var list = await BaseQuery()
                .Where(r => r.Createdat >= start && r.Createdat < end)
                .ToListAsync();

            return list.Select(r => new AdminRatingResponse
            {
                OrderId = r.Orderid,
                FullName = r.User.Fullname,
                Star = r.Star,
                Review = r.Review,
                CreatedAt = r.Createdat ?? DateTime.MinValue
            }).ToList();
        }

        public async Task<List<AdminRatingResponse>> GetRatingsByWeekAsync(HttpContext httpContext, DateTime dateInWeek)
        {
            dateInWeek = DateTime.SpecifyKind(dateInWeek.Date, DateTimeKind.Utc);

            var diff = (7 + (dateInWeek.DayOfWeek - DayOfWeek.Monday)) % 7;
            var start = dateInWeek.AddDays(-diff);
            var end = start.AddDays(7);

            var list = await BaseQuery()
                .Where(r => r.Createdat >= start && r.Createdat < end)
                .ToListAsync();

            return list.Select(r => new AdminRatingResponse
            {
                OrderId = r.Orderid,
                FullName = r.User.Fullname,
                Star = r.Star,
                Review = r.Review,
                CreatedAt = r.Createdat ?? DateTime.MinValue
            }).ToList();
        }

        public async Task<List<AdminRatingResponse>> GetRatingsByMonthAsync(HttpContext httpContext, int year, int month)
        {
            if (month < 1 || month > 12)
                throw new ArgumentException("Tháng phải nằm trong khoảng 1–12.");

            var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);

            var list = await BaseQuery()
                .Where(r => r.Createdat >= start && r.Createdat < end)
                .ToListAsync();

            return list.Select(r => new AdminRatingResponse
            {
                OrderId = r.Orderid,
                FullName = r.User.Fullname,
                Star = r.Star,
                Review = r.Review,
                CreatedAt = r.Createdat ?? DateTime.MinValue
            }).ToList();
        }
        private IQueryable<Rating> BaseQuery() =>
         _unitOfWork.Repository<Rating>()
         .GetAll()
         .Include(r => r.User)
         .OrderByDescending(r => r.Createdat);

    }
}