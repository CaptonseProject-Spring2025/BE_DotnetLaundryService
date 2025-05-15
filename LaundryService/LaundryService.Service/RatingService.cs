using LaundryService.Domain.Entities;
using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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

        public async Task CreateRatingAsync(HttpContext httpContext, string orderId, int starRating, string review)
        {
            var customerId = _util.GetCurrentUserIdOrThrow(httpContext);

            var order = await _unitOfWork.Repository<Order>().GetAsync(o => o.Orderid == orderId);
            if (order == null)
            {
                throw new KeyNotFoundException("Đơn hàng không tồn tại.");
            }

            if (order.Userid != customerId)
            {
                throw new UnauthorizedAccessException("Đơn hàng này không thuộc về bạn. Bạn không thể đánh giá đơn hàng này.");
            }

            if (order.Currentstatus != OrderStatusEnum.DELIVERED.ToString() && order.Currentstatus != OrderStatusEnum.COMPLETED.ToString())
            {
                throw new InvalidOperationException("Bạn chỉ có thể đánh giá đơn hàng khi nó đã được giao hoàn tất (trạng thái DELIVERED hoặc COMPLETED).");
            }

            var existingRating = await _unitOfWork.Repository<Rating>().GetAsync(r => r.Orderid == orderId && r.Userid == customerId);
            if (existingRating != null)
            {
                throw new InvalidOperationException("Bạn đã đánh giá đơn hàng này rồi.");
            }

            var rating = new Rating
            {
                Ratingid = Guid.NewGuid(),
                Userid = customerId,
                Orderid = orderId,
                Star = starRating,
                Review = review,
                Createdat = DateTime.UtcNow
            };

            await _unitOfWork.Repository<Rating>().InsertAsync(rating);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<List<AdminRatingResponse>> GetAllRatingsForAdminAsync(HttpContext httpContext)
        {

            var list = await _unitOfWork.Repository<Rating>()
                .GetAll()
                .OrderByDescending(r =>r.Createdat)
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

        public async Task<double> GetAverageStarAsync(HttpContext httpContext)
        {

            var starsQuery = _unitOfWork.Repository<Rating>()
                .GetAll()
                .Where(r => r.Star.HasValue)
                .Select(r => r.Star.Value);

            if (!await starsQuery.AnyAsync())
                return 0;

            return await starsQuery.AverageAsync();
        }

    }

}
