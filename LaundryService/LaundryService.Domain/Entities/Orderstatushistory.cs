﻿using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Orderstatushistory
{
    public Guid Statushistoryid { get; set; }

    public string Orderid { get; set; } = null!;

    public string? Status { get; set; }

    public string? Statusdescription { get; set; }

    public string? Notes { get; set; }

    public Guid? Updatedby { get; set; }

    public bool? Isfail { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<Orderphoto> Orderphotos { get; set; } = new List<Orderphoto>();

    public virtual User? UpdatedbyNavigation { get; set; }
}
