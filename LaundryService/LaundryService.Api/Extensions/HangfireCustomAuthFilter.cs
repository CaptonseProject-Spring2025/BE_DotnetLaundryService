using Hangfire.Dashboard;

namespace LaundryService.Api.Extensions
{
    public class HangfireCustomAuthFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var http = context.GetHttpContext();
            return http.User.Identity?.IsAuthenticated == true
                   && http.User.IsInRole("Admin");      // chỉ Admin vào dashboard
        }
    }
}
