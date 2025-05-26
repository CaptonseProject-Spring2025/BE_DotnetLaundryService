using System;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Responses;

namespace LaundryService.Service;

public class DashBoardService : IDashBoardServices
{

  private readonly IUnitOfWork _unitOfWork;

  public DashBoardService(IUnitOfWork unitOfWork)
  {
    _unitOfWork = unitOfWork;
  }

  public async Task<int> GetAllOrdersByCurrentStatusAsync()
  {

    //Lấy tất cả các đơn hàng
    var orders = await _unitOfWork.Repository<Order>().GetAllAsync();

    return orders.Count;
  }



  public async Task<int> GetUserCountAsync()
  {
    var customers = await _unitOfWork.Repository<User>().GetAllAsync(
             user => user.Role == "Customer" && user.Status == "Active"
         );
    int customersCount = customers.Count;
    return customersCount;
  }

  public async Task<object> GetOrderStatisticAsync()
  {
    //Lấy ngày hiện tại
    DateTime today = DateTime.Now;
    // Lấy tất cả các đơn hàng
    var orders = await _unitOfWork.Repository<Order>().GetAllAsync();
    // Thống kê theo trạng thái
    var statusStatistics = orders
        .GroupBy(o => o.Currentstatus)
        .Select(g => new
        {
          Status = g.Key,
          Count = g.Count()
        });

    //Đơn hàng trong ngày hôm nay

    var todayOrders = orders.Where(
      o => o.Createdat == today).Count();

    // Đơn hàng trong tuần
    var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
    var weeklyOrders = orders.Where(o => o.Createdat >= startOfWeek && o.Createdat <= today).Count();

    // Đơn hàng trong tháng
    var startOfMonth = new DateTime(today.Year, today.Month, 1);
    var monthlyOrders = orders.Where(o => o.Createdat >= startOfMonth && o.Createdat <= today).Count();

    // Đơn hàng chưa hoàn thành
    var incompleteOrders = orders.Where(o => o.Currentstatus != "Completed").Count();


    // Trả về kết quả
    return new
    {
      StatusStatistics = statusStatistics,
      TodayOrders = todayOrders,
      WeeklyOrders = weeklyOrders,
      MonthlyOrders = monthlyOrders,
      IncompleteOrders = incompleteOrders
    };
  }

  public async Task<int> GetAllServicesAsync()
  {
    //Lấy tất cả các dịch vụ
    var services = _unitOfWork.Repository<Subservice>().GetAllAsync();


    return services.Result.Count;
  }

  public async Task<int> GetAllExtrasAsync()
  {
    //Lấy tất cả các dịch vụ
    var extras = _unitOfWork.Repository<Extra>().GetAllAsync();


    return extras.Result.Count;
  }

  public async Task<object> GetCustomerStatistic()
  {
    // Lấy ngày hiện tại
    var today = DateTime.UtcNow.Date;

    // Lấy tất cả người dùng có role là "Customer" và trạng thái "Active"
    var customers = await _unitOfWork.Repository<User>().GetAllAsync(
        user => user.Role == "Customer" && user.Status == "Active"
    );

    // Số lượng khách hàng mới trong ngày
    var newCustomersToday = customers.Where(c => c.Datecreated == today).Count();

    // Số lượng khách hàng mới trong tuần
    var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
    var newCustomersThisWeek = customers.Where(c => c.Datecreated >= startOfWeek && c.Datecreated <= today).Count();

    // Số lượng khách hàng mới trong tháng
    var startOfMonth = new DateTime(today.Year, today.Month, 1);
    var newCustomersThisMonth = customers.Where(c => c.Datecreated >= startOfMonth && c.Datecreated <= today).Count();

    // Tổng số lượng khách hàng
    var totalCustomers = customers.Count;

    // Trả về kết quả
    return new
    {
      TotalCustomers = totalCustomers,
      NewCustomersToday = newCustomersToday,
      NewCustomersThisWeek = newCustomersThisWeek,
      NewCustomersThisMonth = newCustomersThisMonth
    };
  }

  public async Task<decimal> GetTotalRevenueAsync()
  {
    var paidPayments = await _unitOfWork.Repository<Payment>()
       .GetAllAsync(p => p.Paymentstatus == "PAID");

    return paidPayments.Sum(p => p.Amount);
  }

  public async Task<decimal> GetDailyRevenueAsync(DateTime? date = null)
  {
    // Chuyển đổi ngày sang UTC
    date = date.HasValue
        ? DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc)
        : DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);
    var startDate = date.Value.Date;
    var endDate = startDate.AddDays(1);

    var paidPayments = await _unitOfWork.Repository<Payment>()
        .GetAllAsync(p => p.Paymentstatus == "PAID" &&
                          p.Paymentdate >= startDate &&
                          p.Paymentdate < endDate);

    return paidPayments.Sum(p => p.Amount);
  }

  public async Task<decimal> GetMonthlyRevenueAsync(int month, int? year = null)
  {
    year ??= DateTime.Today.Year;
    var startDate = DateTime.SpecifyKind(new DateTime(year.Value, month, 1), DateTimeKind.Utc);
    var endDate = startDate.AddMonths(1);

    var paidPayments = await _unitOfWork.Repository<Payment>()
        .GetAllAsync(p => p.Paymentstatus == "PAID" &&
                          p.Paymentdate >= startDate &&
                          p.Paymentdate < endDate);

    return paidPayments.Sum(p => p.Amount);
  }

  public async Task<decimal> GetYearlyRevenueAsync(int? year = null)
  {
    year ??= DateTime.Today.Year;
    var startDate = DateTime.SpecifyKind(new DateTime(year.Value, 1, 1), DateTimeKind.Utc);
    var endDate = startDate.AddYears(1);

    var paidPayments = await _unitOfWork.Repository<Payment>()
        .GetAllAsync(p => p.Paymentstatus == "PAID" &&
                          p.Paymentdate >= startDate &&
                          p.Paymentdate < endDate);

    return paidPayments.Sum(p => p.Amount);
  }

public async Task<(decimal Revenue, string Name)> GetRevenueByPaymentMethodAsync(Guid paymentMethodId)
{
    // Lấy danh sách các thanh toán có trạng thái PAID và đúng phương thức
    var paidPayments = await _unitOfWork.Repository<Payment>()
        .GetAllAsync(p => p.Paymentstatus == "PAID" &&
                         p.Paymentmethodid == paymentMethodId);
    
    // Lấy thông tin phương thức thanh toán để lấy tên
    var paymentMethod = await _unitOfWork.Repository<Paymentmethod>()
        .FindAsync(paymentMethodId);
    
    string methodName = paymentMethod?.Name ?? "Unknown";
    
    // Tính tổng doanh thu và trả về kèm tên phương thức
    return (paidPayments.Sum(p => p.Amount), methodName);
}

  public async Task<RevenueDetailStatistic> GetRevenueDetailAsync(DateTime startDate, DateTime endDate, Guid? paymentMethodId = null)
  {
    //lấy các thanh toán có trạng thái PAID
    var payments = await _unitOfWork.Repository<Payment>().GetAllAsync();
    var paidPayments = payments
        .Where(p => p.Paymentstatus == "PAID" &&
               p.Paymentdate >= startDate &&
               p.Paymentdate <= endDate)
        .ToList();

    // Nếu có chỉ định phương thức thanh toán
    if (paymentMethodId.HasValue)
    {
      paidPayments = paidPayments.Where(p => p.Paymentmethodid == paymentMethodId.Value).ToList();
    }

    // Lấy danh sách payment methods để có thông tin tên
    var paymentMethods = await _unitOfWork.Repository<Paymentmethod>().GetAllAsync();
    var methodDict = paymentMethods.ToDictionary(m => m.Paymentmethodid, m => m.Name);

    // Tính tổng doanh thu
    decimal totalRevenue = paidPayments.Sum(p => p.Amount);

    // Tính doanh thu theo phương thức thanh toán
    var revenueByMethod = paidPayments
        .GroupBy(p => p.Paymentmethodid)
        .ToDictionary(
            g => methodDict.ContainsKey(g.Key) ? methodDict[g.Key] : g.Key.ToString(),
            g => g.Sum(p => p.Amount)
        );

    // Tính số lượng giao dịch theo phương thức thanh toán  
    var transactionByMethod = paidPayments
        .GroupBy(p => p.Paymentmethodid)
        .ToDictionary(
            g => methodDict.ContainsKey(g.Key) ? methodDict[g.Key] : g.Key.ToString(),
            g => g.Count()
        );

    // Tính doanh thu theo ngày
    var revenueByDate = paidPayments
        .GroupBy(p => p.Paymentdate?.Date)
        .Where(g => g.Key.HasValue)
        .ToDictionary(
            g => g.Key?.ToString("yyyy-MM-dd") ?? "Unknown",
            g => g.Sum(p => p.Amount)
        );

    return new RevenueDetailStatistic
    {
      TotalRevenue = totalRevenue,
      RevenueByPaymentMethod = revenueByMethod,
      RevenueByDate = revenueByDate,
      TransactionCountByPaymentMethod = transactionByMethod
    };
  }

  public async Task<RevenueTimeStatistic> GetRevenueStatisticByTimeFrameAsync(string timeFrame)
  {
    DateTime today = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);
    DateTime startDate;
    DateTime endDate;

    // Xác định khoảng thời gian
    switch (timeFrame.ToLower())
    {
      case "day":
        startDate = today;
        endDate = today.AddDays(1);
        break;

      case "week":
        startDate = today.AddDays(-(int)today.DayOfWeek);
        endDate = startDate.AddDays(7);
        break;

      case "month":
        startDate = new DateTime(today.Year, today.Month, 1);
        endDate = startDate.AddMonths(1);
        break;

      case "year":
        startDate = new DateTime(today.Year, 1, 1);
        endDate = startDate.AddYears(1);
        break;

      default:
        throw new ArgumentException("Khoảng thời gian không hợp lệ. Sử dụng: day, week, month hoặc year");
    }

    // Lấy tất cả thanh toán đã hoàn thành trong khoảng thời gian
    var payments = await _unitOfWork.Repository<Payment>()
        .GetAllAsync(p => p.Paymentstatus == "PAID" &&
                          p.Paymentdate >= startDate &&
                          p.Paymentdate < endDate);

    // Lấy danh sách phương thức thanh toán
    var paymentMethods = await _unitOfWork.Repository<Paymentmethod>().GetAllAsync();
    var methodDict = paymentMethods.ToDictionary(m => m.Paymentmethodid, m => m.Name);

    // Xác định ID của phương thức thanh toán trực tuyến (PayOS) và tiền mặt (Cash)
    Guid? payOsMethodId = paymentMethods.FirstOrDefault(m => m.Name == "PayOS")?.Paymentmethodid;
    Guid? cashMethodId = paymentMethods.FirstOrDefault(m => m.Name == "Cash")?.Paymentmethodid;

    // Tính tổng doanh thu
    decimal totalRevenue = payments.Sum(p => p.Amount);

    // Doanh thu theo phương thức thanh toán
    decimal onlineRevenue = payments
        .Where(p => payOsMethodId.HasValue && p.Paymentmethodid == payOsMethodId.Value)
        .Sum(p => p.Amount);

    decimal cashRevenue = payments
        .Where(p => cashMethodId.HasValue && p.Paymentmethodid == cashMethodId.Value)
        .Sum(p => p.Amount);

    // Chuẩn bị dictionary cho doanh thu theo giai đoạn
    Dictionary<string, decimal> revenueByPeriod = new Dictionary<string, decimal>();
    Dictionary<string, decimal> onlineRevenueByPeriod = new Dictionary<string, decimal>();
    Dictionary<string, decimal> cashRevenueByPeriod = new Dictionary<string, decimal>();
    Dictionary<string, int> transactionCountByPeriod = new Dictionary<string, int>();

    // Khởi tạo dictionary theo loại thời gian
    InitializePeriodDictionaries(timeFrame, startDate, revenueByPeriod, onlineRevenueByPeriod, cashRevenueByPeriod, transactionCountByPeriod);

    // Phân loại thanh toán theo giai đoạn
    foreach (var payment in payments)
    {
      if (!payment.Paymentdate.HasValue) continue;

      string key = GetPeriodKey(timeFrame, payment.Paymentdate.Value);

      if (revenueByPeriod.ContainsKey(key))
      {
        revenueByPeriod[key] += payment.Amount;
        transactionCountByPeriod[key]++;

        if (payOsMethodId.HasValue && payment.Paymentmethodid == payOsMethodId.Value)
        {
          onlineRevenueByPeriod[key] += payment.Amount;
        }
        else if (cashMethodId.HasValue && payment.Paymentmethodid == cashMethodId.Value)
        {
          cashRevenueByPeriod[key] += payment.Amount;
        }
      }
    }

    return new RevenueTimeStatistic
    {
      TotalRevenue = totalRevenue,
      OnlineRevenue = onlineRevenue,
      CashRevenue = cashRevenue,
      RevenueByPeriod = revenueByPeriod,
      OnlineRevenueByPeriod = onlineRevenueByPeriod,
      CashRevenueByPeriod = cashRevenueByPeriod,
      TransactionCountByPeriod = transactionCountByPeriod
    };
  }


  // Phương thức hỗ trợ để khởi tạo dictionary thời gian
  private void InitializePeriodDictionaries(
      string timeFrame,
      DateTime startDate,
      Dictionary<string, decimal> revenueByPeriod,
      Dictionary<string, decimal> onlineRevenueByPeriod,
      Dictionary<string, decimal> cashRevenueByPeriod,
      Dictionary<string, int> transactionCountByPeriod)
  {
    switch (timeFrame.ToLower())
    {
      case "day":
        // Thống kê theo giờ trong ngày
        for (int i = 0; i < 24; i++)
        {
          string hourKey = $"{i:D2}:00";
          revenueByPeriod.Add(hourKey, 0);
          onlineRevenueByPeriod.Add(hourKey, 0);
          cashRevenueByPeriod.Add(hourKey, 0);
          transactionCountByPeriod.Add(hourKey, 0);
        }
        break;

      case "week":
        // Thống kê theo ngày trong tuần
        for (int i = 0; i < 7; i++)
        {
          string dayName = startDate.AddDays(i).ToString("dddd");
          revenueByPeriod.Add(dayName, 0);
          onlineRevenueByPeriod.Add(dayName, 0);
          cashRevenueByPeriod.Add(dayName, 0);
          transactionCountByPeriod.Add(dayName, 0);
        }
        break;

      case "month":
        // Thống kê theo ngày trong tháng
        int daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
        for (int i = 1; i <= daysInMonth; i++)
        {
          string dayKey = $"{i:D2}";
          revenueByPeriod.Add(dayKey, 0);
          onlineRevenueByPeriod.Add(dayKey, 0);
          cashRevenueByPeriod.Add(dayKey, 0);
          transactionCountByPeriod.Add(dayKey, 0);
        }
        break;

      case "year":
        // Thống kê theo tháng trong năm
        for (int i = 1; i <= 12; i++)
        {
          string monthName = new DateTime(startDate.Year, i, 1).ToString("MMMM");
          revenueByPeriod.Add(monthName, 0);
          onlineRevenueByPeriod.Add(monthName, 0);
          cashRevenueByPeriod.Add(monthName, 0);
          transactionCountByPeriod.Add(monthName, 0);
        }
        break;
    }
  }

  // Phương thức hỗ trợ để tạo key theo thời gian
  private string GetPeriodKey(string timeFrame, DateTime date)
  {
    switch (timeFrame.ToLower())
    {
      case "day":
        return date.ToString("HH:00");
      case "week":
        return date.ToString("dddd");
      case "month":
        return date.Day.ToString("D2");
      case "year":
        return date.ToString("MMMM");
      default:
        return date.ToString("yyyy-MM-dd");
    }
  }

  /// <summary>
  /// Chuyển đổi DateTime sang UTC
  /// </summary>
  private DateTime ToUtcKind(DateTime dateTime)
  {
    return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
  }
}
