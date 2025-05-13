using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Servicedetail
{
    public Guid Serviceid { get; set; }

    public Guid Subserviceid { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string? Image { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual ICollection<Orderitem> Orderitems { get; set; } = new List<Orderitem>();

    public virtual ICollection<Serviceextramapping> Serviceextramappings { get; set; } = new List<Serviceextramapping>();

    public virtual Subservice Subservice { get; set; } = null!;
}
