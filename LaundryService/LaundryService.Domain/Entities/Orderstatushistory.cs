using LaundryService.Domain.Enums;
using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Orderstatushistory
{
    public Guid Statushistoryid { get; set; }

    public Guid Orderid { get; set; }

    public OrderStatusEnum? Status { get; set; }

    public string? Statusdescription { get; set; }

    public Guid? Updatedby { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual User? UpdatedbyNavigation { get; set; }
}
