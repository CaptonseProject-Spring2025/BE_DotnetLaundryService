using System;

namespace LaundryService.Dto.Responses;

/// <summary>
/// Thống kê chi tiết doanh thu
/// </summary>
public class RevenueDetailStatistic
{
  /// <summary>
  /// Tổng doanh thu
  /// </summary>
  public decimal TotalRevenue { get; set; }

  /// <summary>
  /// Doanh thu theo phương thức thanh toán
  /// </summary>
  public Dictionary<string, decimal> RevenueByPaymentMethod { get; set; }

  /// <summary>
  /// Doanh thu theo ngày
  /// </summary>
  public Dictionary<string, decimal> RevenueByDate { get; set; }

  /// <summary>
  /// Số lượng giao dịch theo phương thức thanh toán
  /// </summary>
  public Dictionary<string, int> TransactionCountByPaymentMethod { get; set; }
}
