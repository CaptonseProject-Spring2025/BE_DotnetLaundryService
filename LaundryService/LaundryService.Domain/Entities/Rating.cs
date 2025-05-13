using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Rating
{
    public Guid Ratingid { get; set; }

    public Guid Userid { get; set; }

    public string Orderid { get; set; } = null!;

    public int? Star { get; set; }

    public string? Review { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
