using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Orderitem
{
    public Guid Orderitemid { get; set; }

    public Guid Orderid { get; set; }

    public Guid Serviceid { get; set; }

    public int Quantity { get; set; }

    public decimal? Baseprice { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<Orderextra> Orderextras { get; set; } = new List<Orderextra>();

    public virtual Servicedetail Service { get; set; } = null!;
}
