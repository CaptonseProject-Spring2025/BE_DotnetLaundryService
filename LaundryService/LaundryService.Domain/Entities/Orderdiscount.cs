using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Orderdiscount
{
    public Guid Orderdiscountid { get; set; }

    public Guid Orderid { get; set; }

    public Guid Discountcodeid { get; set; }

    public DateTime? Appliedat { get; set; }

    public decimal? Discountamount { get; set; }

    public virtual Discountcode Discountcode { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
