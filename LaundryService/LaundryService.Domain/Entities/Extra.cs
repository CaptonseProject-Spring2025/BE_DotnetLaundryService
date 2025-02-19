using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Extra
{
    public Guid Extraid { get; set; }

    public Guid Extracategoryid { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string? Image { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual Extracategory Extracategory { get; set; } = null!;

    public virtual ICollection<Orderextra> Orderextras { get; set; } = new List<Orderextra>();

    public virtual ICollection<Serviceextramapping> Serviceextramappings { get; set; } = new List<Serviceextramapping>();
}
