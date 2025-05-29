using System;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;

namespace LaundryService.Domain.Interfaces;

public interface IStaffStatisticsService
{
  Task<StaffStatisticsResponse> GetStaffStatisticsAsync(HttpContext httpContext, DateTime? startDate = null, DateTime? endDate = null);
  Task<WorkflowStatistics> GetWorkflowStatisticsAsync(HttpContext httpContext);
  Task<PerformanceStatistics> GetPerformanceStatisticsAsync(HttpContext httpContext);
  Task<List<StaffDailyStatistics>> GetDailyStatisticsAsync(HttpContext httpContext, DateTime date);
  Task<List<StaffDailyStatistics>> GetWeeklyStatisticsAsync(HttpContext httpContext, DateTime weekStart);
  Task<List<StaffDailyStatistics>> GetMonthlyStatisticsAsync(HttpContext httpContext, int year, int month);
  Task<StaffWorkloadStatistics> GetWorkloadStatisticsAsync(HttpContext httpContext);
}
