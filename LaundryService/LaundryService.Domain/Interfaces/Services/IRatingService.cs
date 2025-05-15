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
        Task CreateRatingAsync(HttpContext httpContext, string orderId, int starRating, string review);
        Task<List<AdminRatingResponse>> GetAllRatingsForAdminAsync(HttpContext httpContext);
        Task<double> GetAverageStarAsync(HttpContext httpContext);
    }
}
