using LaundryService.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Infrastructure
{
    public class Util : IUtil
    {
        /// <summary>
        /// Lấy userId từ JWT token. Ném exception nếu không hợp lệ.
        /// </summary>
        public Guid GetCurrentUserIdOrThrow(HttpContext httpContext)
        {
            var userIdClaim = httpContext?.User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var userId) && userId != Guid.Empty)
            {
                return userId;
            }
            throw new UnauthorizedAccessException("Invalid token: Cannot retrieve userId.");
        }

        // Hàm convert DateTime UTC sang giờ Việt Nam (UTC+7)
        public DateTime ConvertToVnTime(DateTime utcDateTime)
        {
            // Cách 1: utcDateTime.AddHours(7)
            // Cách 2: Sử dụng TimeZoneInfo 
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, timeZone);
        }
    }
}
