using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Notification
{
    public Guid Notificationid { get; set; }

    public Guid Userid { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string? Notificationtype { get; set; }

    public bool? Isread { get; set; }

    public Guid? Customerid { get; set; }

    public string? Orderid { get; set; }

    public DateTime? Createdat { get; set; }

    public bool? Ispushenabled { get; set; }

    public virtual User User { get; set; } = null!;
}
