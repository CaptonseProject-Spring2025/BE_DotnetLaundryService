using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Rewardtransaction
{
    public Guid Rewardtransactionid { get; set; }

    public Guid Userid { get; set; }

    public Guid? Orderid { get; set; }

    public string? Transactiontype { get; set; }

    public int Points { get; set; }

    public Guid? Optionid { get; set; }

    public DateTime? Transactiondate { get; set; }

    public string? Note { get; set; }

    public virtual Rewardredemptionoption? Option { get; set; }

    public virtual User User { get; set; } = null!;
}
