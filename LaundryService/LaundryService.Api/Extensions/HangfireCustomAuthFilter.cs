using Hangfire.Dashboard;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace LaundryService.Api.Extensions
{
    public class HangfireCustomAuthFilter : IDashboardAuthorizationFilter
    {
        private readonly string _secret;
        public HangfireCustomAuthFilter(string secret) => _secret = secret;

        public bool Authorize(DashboardContext context)
        {
            var http = context.GetHttpContext();
            string? token = http.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "")
                          ?? http.Request.Query["access_token"];

            if (string.IsNullOrEmpty(token)) return false;

            var handler = new JwtSecurityTokenHandler();
            var validation = new TokenValidationParameters
            {
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                var principal = handler.ValidateToken(token, validation, out _);
                // Bạn muốn chỉ Admin:
                if (!principal.IsInRole("Admin")) return false;

                http.User = principal;          // gắn vào HttpContext
                return true;
            }
            catch { return false; }
        }
    }
}
