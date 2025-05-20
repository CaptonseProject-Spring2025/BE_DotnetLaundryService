using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Absentdriver
{
    public Guid Absentid { get; set; }

    public Guid Driverid { get; set; }

    public DateOnly Dateabsent { get; set; }

    public DateTime Absentfrom { get; set; }

    public DateTime Absentto { get; set; }

    public DateTime? Datecreated { get; set; }

    public virtual User Driver { get; set; } = null!;
}
