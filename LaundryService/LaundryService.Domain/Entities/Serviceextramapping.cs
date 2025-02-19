using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Serviceextramapping
{
    public Guid Mappingid { get; set; }

    public Guid Serviceid { get; set; }

    public Guid Extraid { get; set; }

    public virtual Extra Extra { get; set; } = null!;

    public virtual Servicedetail Service { get; set; } = null!;
}
