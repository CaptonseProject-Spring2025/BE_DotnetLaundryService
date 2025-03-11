using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Orderstatushistory
{
    public Guid Statushistoryid { get; set; }

    public Guid Orderid { get; set; }

    public DateTime? Assignedat { get; set; }

    public string? Status { get; set; }

    public string? Statusdescription { get; set; }

    public string? Notes { get; set; }

    public Guid? Updatedby { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<Orderphoto> Orderphotos { get; set; } = new List<Orderphoto>();

    public virtual User? UpdatedbyNavigation { get; set; }
}
