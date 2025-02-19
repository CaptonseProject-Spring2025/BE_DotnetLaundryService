using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Address
{
    public Guid Addressid { get; set; }

    public Guid Userid { get; set; }

    public string Addresslabel { get; set; } = null!;

    public string Contactname { get; set; } = null!;

    public string Contactphone { get; set; } = null!;

    public string Detailaddress { get; set; } = null!;

    public string? Description { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public bool? Isdefault { get; set; }

    public DateTime? Datecreated { get; set; }

    public DateTime? Datemodified { get; set; }

    public virtual User User { get; set; } = null!;
}
