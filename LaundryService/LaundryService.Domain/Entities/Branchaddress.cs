using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Branchaddress
{
    public Guid Brachid { get; set; }

    public string? Addressdetail { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }
}
