using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Orderextra
{
    public Guid Orderextraid { get; set; }

    public Guid Orderitemid { get; set; }

    public Guid Extraid { get; set; }

    public decimal Extraprice { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual Extra Extra { get; set; } = null!;

    public virtual Orderitem Orderitem { get; set; } = null!;
}
