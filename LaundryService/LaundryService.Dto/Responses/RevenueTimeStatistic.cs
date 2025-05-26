using System;

namespace LaundryService.Dto.Responses;


/// <summary>
/// Thống kê doanh thu theo thời gian
/// </summary>
public class RevenueTimeStatistic
{
  /// <summary>
  /// Tổng doanh thu
  /// </summary>
  public decimal TotalRevenue { get; set; }

  /// <summary>
  /// Doanh thu từ thanh toán trực tuyến (PayOS)
  /// </summary>
  public decimal OnlineRevenue { get; set; }

  /// <summary>
  /// Doanh thu từ thanh toán tiền mặt (Cash)
  /// </summary>
  public decimal CashRevenue { get; set; }

  /// <summary>
  /// Doanh thu theo từng giai đoạn (theo giờ/ngày/tháng tùy thuộc vào timeFrame)
  /// </summary>
  public Dictionary<string, decimal> RevenueByPeriod { get; set; }

  /// <summary>
  /// Doanh thu trực tuyến theo từng giai đoạn
  /// </summary>
  public Dictionary<string, decimal> OnlineRevenueByPeriod { get; set; }

  /// <summary>
  /// Doanh thu tiền mặt theo từng giai đoạn
  /// </summary>
  public Dictionary<string, decimal> CashRevenueByPeriod { get; set; }

  /// <summary>
  /// Số lượng giao dịch theo từng giai đoạn
  /// </summary>
  public Dictionary<string, int> TransactionCountByPeriod { get; set; }
}
