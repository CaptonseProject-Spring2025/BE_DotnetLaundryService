using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Area
{
    public Guid Areaid { get; set; }

    public string Name { get; set; } = null!;

    public List<string>? Districts { get; set; }

    public string Areatype { get; set; } = null!;

    public decimal? Shippingfee { get; set; }
}
