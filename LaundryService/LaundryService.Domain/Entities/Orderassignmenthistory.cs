using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Orderassignmenthistory
{
    public Guid Assignmentid { get; set; }

    public Guid Orderid { get; set; }

    public Guid Assignedto { get; set; }

    public string? Role { get; set; }

    public DateTime? Assignedat { get; set; }

    public string? Status { get; set; }

    public string? Declinereason { get; set; }

    public DateTime? Completedat { get; set; }

    public virtual User AssignedtoNavigation { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
