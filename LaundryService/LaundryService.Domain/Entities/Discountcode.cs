using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Discountcode
{
    public Guid Discountcodeid { get; set; }

    public string Code { get; set; } = null!;

    public string? Description { get; set; }

    public string? Discounttype { get; set; }

    public decimal Value { get; set; }

    public string? Appliesto { get; set; }

    public decimal? Minimumordervalue { get; set; }

    public decimal? Maximumdiscount { get; set; }

    public int? Usagelimit { get; set; }

    public int? Usageperuser { get; set; }

    public DateTime? Startdate { get; set; }

    public DateTime? Enddate { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual ICollection<Orderdiscount> Orderdiscounts { get; set; } = new List<Orderdiscount>();
}
