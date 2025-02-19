using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Paymentmethod
{
    public Guid Paymentmethodid { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool? Isactive { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
