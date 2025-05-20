using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Rewardhistory
{
    public Guid Rewardhistoryid { get; set; }

    public Guid Userid { get; set; }

    public string? Orderid { get; set; }

    public string? Ordername { get; set; }

    public int Points { get; set; }

    public DateTime? Datecreated { get; set; }

    public virtual Order? Order { get; set; }

    public virtual User User { get; set; } = null!;
}
