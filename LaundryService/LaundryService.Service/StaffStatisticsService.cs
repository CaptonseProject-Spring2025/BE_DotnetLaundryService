using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LaundryService.Service
{
  public class StaffStatisticsService : IStaffStatisticsService
  {
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUtil _util;

    public StaffStatisticsService(IUnitOfWork unitOfWork, IUtil util)
    {
      _unitOfWork = unitOfWork;
      _util = util;
    }

    public async Task<StaffStatisticsResponse> GetStaffStatisticsAsync(
        HttpContext httpContext,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
      var staffId = _util.GetCurrentUserIdOrThrow(httpContext);

      // Mặc định lấy thống kê 30 ngày gần nhất
      startDate ??= DateTime.UtcNow.AddDays(-30);
      endDate ??= DateTime.UtcNow;

      var stats = new StaffStatisticsResponse();

      // Thống kê tổng quan
      await PopulateOverallStatistics(stats, staffId, startDate.Value, endDate.Value);

      // Thống kê workflow
      stats.Workflow = await GetWorkflowStatisticsAsync(httpContext);

      // Thống kê hiệu suất
      stats.Performance = await GetPerformanceStatisticsAsync(httpContext);

      return stats;
    }

    private async Task PopulateOverallStatistics(
        StaffStatisticsResponse stats,
        Guid staffId,
        DateTime startDate,
        DateTime endDate)
    {
      // Lấy tất cả OrderStatusHistory mà staff đã cập nhật
      var staffHistories = await _unitOfWork.Repository<Orderstatushistory>()
          .GetAll()
          .Where(h => h.Updatedby == staffId &&
                     h.Createdat >= startDate &&
                     h.Createdat <= endDate)
          .Include(h => h.Order)
          .ToListAsync();

      // Đếm theo trạng thái
      stats.OrdersInChecking = staffHistories.Count(h => h.Status == "CHECKING");
      stats.OrdersInWashing = staffHistories.Count(h => h.Status == "WASHING");
      stats.OrdersInWashed = staffHistories.Count(h => h.Status == "WASHED");
      stats.OrdersQualityChecked = staffHistories.Count(h => h.Status == "QUALITY_CHECKED");

      // Tổng đơn đã xử lý
      stats.TotalOrdersProcessed = staffHistories.Select(h => h.Orderid).Distinct().Count();

      // Đơn hoàn thành (đã đến DELIVERED hoặc COMPLETED)
      var completedOrderIds = staffHistories
          .Where(h => h.Status == "DELIVERED" || h.Status == "COMPLETED")
          .Select(h => h.Orderid)
          .Distinct();
      stats.OrdersCompleted = completedOrderIds.Count();

      // Thống kê theo thời gian
      var today = DateTime.UtcNow.Date;
      var weekStart = today.AddDays(-(int)today.DayOfWeek);
      var monthStart = new DateTime(today.Year, today.Month, 1);

      stats.OrdersCompletedToday = staffHistories
          .Count(h => (h.Status == "DELIVERED" || h.Status == "COMPLETED") &&
                     h.Createdat.Value.Date == today);

      stats.OrdersCompletedThisWeek = staffHistories
          .Count(h => (h.Status == "DELIVERED" || h.Status == "COMPLETED") &&
                     h.Createdat >= weekStart);

      stats.OrdersCompletedThisMonth = staffHistories
          .Count(h => (h.Status == "DELIVERED" || h.Status == "COMPLETED") &&
                     h.Createdat >= monthStart);

      // Tính thời gian xử lý trung bình
      await CalculateAverageProcessingTime(stats, staffId, startDate, endDate);
    }

    private async Task CalculateAverageProcessingTime(
        StaffStatisticsResponse stats,
        Guid staffId,
        DateTime startDate,
        DateTime endDate)
    {
      var processingTimes = await _unitOfWork.Repository<Orderstatushistory>()
          .GetAll()
          .Where(h => h.Updatedby == staffId &&
                     h.Createdat >= startDate &&
                     h.Createdat <= endDate)
          .GroupBy(h => h.Orderid)
          .Select(g => new
          {
            OrderId = g.Key,
            StartTime = g.Min(x => x.Createdat),
            EndTime = g.Max(x => x.Createdat)
          })
          .ToListAsync();

      if (processingTimes.Any())
      {
        var avgTicks = processingTimes
            .Where(pt => pt.EndTime > pt.StartTime)
            .Average(pt => (pt.EndTime - pt.StartTime)?.Ticks);

        stats.AverageProcessingTimeHours = TimeSpan.FromTicks((long)avgTicks).TotalHours;
      }
    }

    public async Task<WorkflowStatistics> GetWorkflowStatisticsAsync(HttpContext httpContext)
    {
      var staffId = _util.GetCurrentUserIdOrThrow(httpContext);
      var today = DateTime.UtcNow.Date;
      var weekStart = today.AddDays(-(int)today.DayOfWeek);

      var stats = new WorkflowStatistics();

      // Đơn hiện tại đang checking
      stats.OrdersCurrentlyChecking = await _unitOfWork.Repository<Order>()
          .GetAll()
          .CountAsync(o => o.Currentstatus == "CHECKING" &&
                         o.Orderstatushistories.Any(h => h.Updatedby == staffId && h.Status == "CHECKING"));

      // Đơn hiện tại đang washing
      stats.OrdersCurrentlyWashing = await _unitOfWork.Repository<Order>()
          .GetAll()
          .CountAsync(o => o.Currentstatus == "WASHING" &&
                         o.Orderstatushistories.Any(h => h.Updatedby == staffId && h.Status == "WASHING"));

      // Thống kê hôm nay
      stats.OrdersCheckedToday = await _unitOfWork.Repository<Orderstatushistory>()
          .GetAll()
          .CountAsync(h => h.Updatedby == staffId &&
                         h.Status == "CHECKED" &&
                         h.Createdat.Value.Date == today);

      stats.OrdersWashedToday = await _unitOfWork.Repository<Orderstatushistory>()
          .GetAll()
          .CountAsync(h => h.Updatedby == staffId &&
                         h.Status == "WASHED" &&
                         h.Createdat.Value.Date == today);

      stats.OrdersQualityCheckedToday = await _unitOfWork.Repository<Orderstatushistory>()
          .GetAll()
          .CountAsync(h => h.Updatedby == staffId &&
                         h.Status == "QUALITY_CHECKED" &&
                         h.Createdat.Value.Date == today);

      // Thống kê tuần này
      stats.OrdersCheckedThisWeek = await _unitOfWork.Repository<Orderstatushistory>()
          .GetAll()
          .CountAsync(h => h.Updatedby == staffId &&
                         h.Status == "CHECKED" &&
                         h.Createdat >= weekStart);

      stats.OrdersWashedThisWeek = await _unitOfWork.Repository<Orderstatushistory>()
          .GetAll()
          .CountAsync(h => h.Updatedby == staffId &&
                         h.Status == "WASHED" &&
                         h.Createdat >= weekStart);

      stats.OrdersQualityCheckedThisWeek = await _unitOfWork.Repository<Orderstatushistory>()
          .GetAll()
          .CountAsync(h => h.Updatedby == staffId &&
                         h.Status == "QUALITY_CHECKED" &&
                         h.Createdat >= weekStart);

      return stats;
    }

    public async Task<PerformanceStatistics> GetPerformanceStatisticsAsync(HttpContext httpContext)
    {
      var staffId = _util.GetCurrentUserIdOrThrow(httpContext);
      var today = DateTime.UtcNow.Date;
      var weekStart = today.AddDays(-(int)today.DayOfWeek);

      var stats = new PerformanceStatistics();

      // Thống kê ảnh đã upload
      stats.PhotosUploadedToday = await _unitOfWork.Repository<Orderphoto>()
          .GetAll()
          .CountAsync(p => p.Statushistory.Updatedby == staffId &&
                         p.Createdat.Value.Date == today);

      stats.PhotosUploadedThisWeek = await _unitOfWork.Repository<Orderphoto>()
          .GetAll()
          .CountAsync(p => p.Statushistory.Updatedby == staffId &&
                         p.Createdat >= weekStart);

      stats.TotalPhotosUploaded = await _unitOfWork.Repository<Orderphoto>()
          .GetAll()
          .CountAsync(p => p.Statushistory.Updatedby == staffId);

      // Tính thời gian trung bình cho từng giai đoạn
      await CalculatePhaseAverageTimes(stats, staffId);

      // Tính điểm hiệu suất (dựa trên số đơn hoàn thành / thời gian)
      await CalculateProductivityScore(stats, staffId);

      return stats;
    }

    private async Task CalculatePhaseAverageTimes(PerformanceStatistics stats, Guid staffId)
    {
      var last30Days = DateTime.UtcNow.AddDays(-30);

      // Thời gian checking trung bình
      var checkingTimes = await _unitOfWork.Repository<Orderstatushistory>()
          .GetAll()
          .Where(h => h.Updatedby == staffId &&
                     h.Status == "CHECKING" &&
                     h.Createdat >= last30Days)
          .GroupBy(h => h.Orderid)
          .Select(g => new
          {
            StartTime = g.Min(x => x.Createdat),
            EndTime = _unitOfWork.Repository<Orderstatushistory>()
                  .GetAll()
                  .Where(h2 => h2.Orderid == g.Key && h2.Status == "CHECKED")
                  .Min(x => x.Createdat)
          })
          .Where(x => x.EndTime > x.StartTime)
          .ToListAsync();

      if (checkingTimes.Any())
      {
        var avgCheckingTicks = checkingTimes.Average(ct => (ct.EndTime - ct.StartTime)?.Ticks);
        stats.AverageCheckingTimeHours = TimeSpan.FromTicks((long)avgCheckingTicks).TotalHours;
      }

      // Thời gian washing trung bình (tương tự)
      var washingTimes = await _unitOfWork.Repository<Orderstatushistory>()
          .GetAll()
          .Where(h => h.Updatedby == staffId &&
                     h.Status == "WASHING" &&
                     h.Createdat >= last30Days)
          .GroupBy(h => h.Orderid)
          .Select(g => new
          {
            StartTime = g.Min(x => x.Createdat),
            EndTime = _unitOfWork.Repository<Orderstatushistory>()
                  .GetAll()
                  .Where(h2 => h2.Orderid == g.Key && h2.Status == "WASHED")
                  .Min(x => x.Createdat)
          })
          .Where(x => x.EndTime > x.StartTime)
          .ToListAsync();

      if (washingTimes.Any())
      {
        var avgWashingTicks = washingTimes.Average(wt => (wt.EndTime - wt.StartTime)?.Ticks);
        stats.AverageWashingTimeHours = TimeSpan.FromTicks((long)avgWashingTicks).TotalHours;
      }
    }

    private async Task CalculateProductivityScore(PerformanceStatistics stats, Guid staffId)
    {
      var last30Days = DateTime.UtcNow.AddDays(-30);

      var completedOrders = await _unitOfWork.Repository<Orderstatushistory>()
          .GetAll()
          .CountAsync(h => h.Updatedby == staffId &&
                         (h.Status == "DELIVERED" || h.Status == "COMPLETED") &&
                         h.Createdat >= last30Days);

      var workingDays = 30; // Có thể tính chính xác hơn dựa trên lịch làm việc
      stats.ProductivityScore = (double)completedOrders / workingDays * 10; // Nhân 10 để có điểm từ 0-100
    }

    public async Task<List<StaffDailyStatistics>> GetDailyStatisticsAsync(HttpContext httpContext, DateTime date)
    {
      var staffId = _util.GetCurrentUserIdOrThrow(httpContext);
      var startOfDay = date.Date;
      var endOfDay = startOfDay.AddDays(1);

      var dailyStats = await _unitOfWork.Repository<Orderstatushistory>()
          .GetAll()
          .Where(h => h.Updatedby == staffId &&
                     h.Createdat >= startOfDay &&
                     h.Createdat < endOfDay)
          .GroupBy(h => h.Createdat.Value.Date)
          .Select(g => new StaffDailyStatistics
          {
            Date = g.Key,
            OrdersProcessed = g.Select(x => x.Orderid).Distinct().Count(),
            OrdersCompleted = g.Count(x => x.Status == "DELIVERED" || x.Status == "COMPLETED"),
            PhotosUploaded = g.Sum(x => x.Orderphotos.Count())
          })
          .ToListAsync();

      return dailyStats;
    }

    public async Task<List<StaffDailyStatistics>> GetWeeklyStatisticsAsync(HttpContext httpContext, DateTime weekStart)
    {
      var staffId = _util.GetCurrentUserIdOrThrow(httpContext);
      var weekEnd = weekStart.AddDays(7);

      var weeklyStats = await _unitOfWork.Repository<Orderstatushistory>()
          .GetAll()
          .Where(h => h.Updatedby == staffId &&
                     h.Createdat >= weekStart &&
                     h.Createdat < weekEnd)
          .GroupBy(h => h.Createdat.Value.Date)
          .Select(g => new StaffDailyStatistics
          {
            Date = g.Key,
            OrdersProcessed = g.Select(x => x.Orderid).Distinct().Count(),
            OrdersCompleted = g.Count(x => x.Status == "DELIVERED" || x.Status == "COMPLETED"),
            PhotosUploaded = g.Sum(x => x.Orderphotos.Count())
          })
          .OrderBy(s => s.Date)
          .ToListAsync();

      return weeklyStats;
    }

    public async Task<List<StaffDailyStatistics>> GetMonthlyStatisticsAsync(HttpContext httpContext, int year, int month)
    {
      var staffId = _util.GetCurrentUserIdOrThrow(httpContext);
      var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
      var monthEnd = monthStart.AddMonths(1);

      var monthlyStats = await _unitOfWork.Repository<Orderstatushistory>()
          .GetAll()
          .Where(h => h.Updatedby == staffId &&
                     h.Createdat >= monthStart &&
                     h.Createdat < monthEnd)
          .GroupBy(h => h.Createdat.Value.Date)
          .Select(g => new StaffDailyStatistics
          {
            Date = g.Key,
            OrdersProcessed = g.Select(x => x.Orderid).Distinct().Count(),
            OrdersCompleted = g.Count(x => x.Status == "DELIVERED" || x.Status == "COMPLETED"),
            PhotosUploaded = g.Sum(x => x.Orderphotos.Count())
          })
          .OrderBy(s => s.Date)
          .ToListAsync();

      return monthlyStats;
    }

    public async Task<StaffWorkloadStatistics> GetWorkloadStatisticsAsync(HttpContext httpContext)
    {
      var staffId = _util.GetCurrentUserIdOrThrow(httpContext);

      var stats = new StaffWorkloadStatistics();

      // Đơn khẩn cấp đang xử lý
      stats.EmergencyOrders = await _unitOfWork.Repository<Order>()
          .GetAll()
          .CountAsync(o => o.Emergency == true &&
                         o.Orderstatushistories.Any(h => h.Updatedby == staffId &&
                         (o.Currentstatus == "CHECKING" || o.Currentstatus == "WASHING" || o.Currentstatus == "WASHED")));

      // Đơn bình thường
      stats.NormalOrders = await _unitOfWork.Repository<Order>()
          .GetAll()
          .CountAsync(o => (o.Emergency == false || o.Emergency == null) &&
                         o.Orderstatushistories.Any(h => h.Updatedby == staffId &&
                         (o.Currentstatus == "CHECKING" || o.Currentstatus == "WASHING" || o.Currentstatus == "WASHED")));

      // Đơn quá hạn (delivery time đã qua)
      stats.OverdueOrders = await _unitOfWork.Repository<Order>()
          .GetAll()
          .CountAsync(o => o.Deliverytime < DateTime.UtcNow &&
                         o.Orderstatushistories.Any(h => h.Updatedby == staffId &&
                         (o.Currentstatus == "CHECKING" || o.Currentstatus == "WASHING" || o.Currentstatus == "WASHED")));

      // Thời gian giao hàng gần nhất
      stats.NextDeliveryTime = await _unitOfWork.Repository<Order>()
          .GetAll()
          .Where(o => o.Deliverytime > DateTime.UtcNow &&
                     o.Orderstatushistories.Any(h => h.Updatedby == staffId &&
                     (o.Currentstatus == "CHECKING" || o.Currentstatus == "WASHING" || o.Currentstatus == "WASHED")))
          .MinAsync(o => (DateTime?)o.Deliverytime);

      // Tổng đơn đang pending
      stats.PendingOrders = stats.EmergencyOrders + stats.NormalOrders;

      return stats;
    }
  }
}