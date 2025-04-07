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

        // Hàm tạo mã đơn hàng ngẫu nhiên
        public string GenerateOrderId()
        {
            // 1) Lấy ngày hiện tại
            var now = DateTime.UtcNow;  // Hoặc DateTime.Now tùy bạn
            var datePart = now.ToString("yyMMdd");
            // => "230607" (năm cuối 2 số, tháng, ngày)

            // 2) Sinh 6 ký tự random (A-Z, 0-9)
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var sb = new StringBuilder();
            for (int i = 0; i < 6; i++)
            {
                var index = random.Next(chars.Length);
                sb.Append(chars[index]);
            }
            var randomPart = sb.ToString();  // ví dụ: "X7MZ0A"

            return $"{datePart}{randomPart}";
        }
    }
}
