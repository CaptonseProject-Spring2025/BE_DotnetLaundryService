using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Discountcodeuser
{
    public Guid Id { get; set; }

    public Guid Discountcodeid { get; set; }

    public Guid Userid { get; set; }

    public DateTime? Assignedat { get; set; }

    public virtual Discountcode Discountcode { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
