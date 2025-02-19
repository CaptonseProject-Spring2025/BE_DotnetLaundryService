using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Membershiptier
{
    public Guid Tierid { get; set; }

    public string Tiername { get; set; } = null!;

    public decimal Lowerbound { get; set; }

    public decimal? Upperbound { get; set; }

    public string? Description { get; set; }

    public bool? Isactive { get; set; }

    public DateTime? Createdat { get; set; }

    public DateTime? Updatedat { get; set; }

    public virtual ICollection<Usermembershipstatus> Usermembershipstatuses { get; set; } = new List<Usermembershipstatus>();
}
