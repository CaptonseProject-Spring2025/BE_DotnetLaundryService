using System;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;

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
}
