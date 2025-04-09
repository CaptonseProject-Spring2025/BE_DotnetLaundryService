using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Orderphoto
{
    public Guid Photoid { get; set; }

    public Guid Statushistoryid { get; set; }

    public string Photourl { get; set; } = null!;

    public DateTime? Createdat { get; set; }

    public virtual Orderstatushistory Statushistory { get; set; } = null!;
}
