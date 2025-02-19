using LaundryService.Domain.Enums;
using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Orderdriver
{
    public Guid Id { get; set; }

    public Guid Orderid { get; set; }

    public Guid Driverid { get; set; }

    public DriverRoleEnum? Role { get; set; }

    public DateTime? Assignedat { get; set; }

    public virtual User Driver { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
