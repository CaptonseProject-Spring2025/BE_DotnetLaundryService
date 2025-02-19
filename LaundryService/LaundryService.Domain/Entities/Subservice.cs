using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Subservice
{
    public Guid Subserviceid { get; set; }

    public Guid Categoryid { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual Servicecategory Category { get; set; } = null!;

    public virtual ICollection<Servicedetail> Servicedetails { get; set; } = new List<Servicedetail>();
}
