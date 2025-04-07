using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Driverlocationhistory
{
    public Guid Historyid { get; set; }

    public Guid Driverid { get; set; }

    public string? Orderid { get; set; }

    public decimal Latitudep { get; set; }

    public decimal Longitude { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual User Driver { get; set; } = null!;

    public virtual Order? Order { get; set; }
}
