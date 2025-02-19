using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Extracategory
{
    public Guid Extracategoryid { get; set; }

    public string Name { get; set; } = null!;

    public DateTime? Createdat { get; set; }

    public virtual ICollection<Extra> Extras { get; set; } = new List<Extra>();
}
