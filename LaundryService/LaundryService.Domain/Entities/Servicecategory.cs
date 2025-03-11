using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Servicecategory
{
    public Guid Categoryid { get; set; }

    public string Name { get; set; } = null!;

    public string? Icon { get; set; }

    public string? Banner { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual ICollection<Subservice> Subservices { get; set; } = new List<Subservice>();
}
