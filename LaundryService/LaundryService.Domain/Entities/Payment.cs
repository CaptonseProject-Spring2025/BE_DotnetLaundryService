using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Payment
{
    public Guid Paymentid { get; set; }

    public string Orderid { get; set; } = null!;

    public DateTime? Paymentdate { get; set; }

    public decimal Amount { get; set; }

    public Guid Paymentmethodid { get; set; }

    public string? Paymentstatus { get; set; }

    public string? Transactionid { get; set; }

    public string? Paymentmetadata { get; set; }

    public DateTime? Createdat { get; set; }

    public DateTime? Updatedat { get; set; }

    public Guid? Collectedby { get; set; }

    public bool Isreturnedtoadmin { get; set; }

    public virtual User? CollectedbyNavigation { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Paymentmethod Paymentmethod { get; set; } = null!;
}
