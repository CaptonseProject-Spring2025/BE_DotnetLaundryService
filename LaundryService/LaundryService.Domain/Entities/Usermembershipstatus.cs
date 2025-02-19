using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Usermembershipstatus
{
    public Guid Usermembershipid { get; set; }

    public Guid Userid { get; set; }

    public Guid Tierid { get; set; }

    public decimal Totalspending { get; set; }

    public DateTime? Lastupdated { get; set; }

    public virtual Membershiptier Tier { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
