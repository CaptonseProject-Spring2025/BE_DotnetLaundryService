using System;

namespace LaundryService.Infrastructure;

public static class DateTimeExtensions
{
  public static DateTime EnsureUtc(this DateTime dateTime)
  {
    return dateTime.Kind == DateTimeKind.Utc
        ? dateTime
        : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
  }

  public static DateTime? EnsureUtc(this DateTime? dateTime)
  {
    return dateTime?.EnsureUtc();
  }
}
