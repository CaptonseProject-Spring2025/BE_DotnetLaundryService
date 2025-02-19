using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Orderphoto
{
    public Guid Photoid { get; set; }

    public Guid Orderid { get; set; }

    public Guid Driverid { get; set; }

    public string Phototype { get; set; } = null!;

    public string Photourl { get; set; } = null!;

    public int? Photosequence { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual User Driver { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
