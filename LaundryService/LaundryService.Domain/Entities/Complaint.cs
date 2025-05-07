using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Complaint
{
    public Guid Complaintid { get; set; }

    public string Orderid { get; set; } = null!;

    public Guid Userid { get; set; }

    public string? Complainttype { get; set; }

    public string Complaintdescription { get; set; } = null!;

    public string Status { get; set; } = null!;

    public Guid? Assignedto { get; set; }

    public string? Resolutiondetails { get; set; }

    public DateTime? Createdat { get; set; }

    public DateTime? Resolvedat { get; set; }

    public virtual User? AssignedtoNavigation { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
