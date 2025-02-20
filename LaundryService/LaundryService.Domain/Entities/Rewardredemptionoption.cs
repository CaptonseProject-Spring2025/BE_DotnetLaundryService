using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Rewardredemptionoption
{
    public Guid Optionid { get; set; }

    public decimal Discountamount { get; set; }

    public int Requiredpoints { get; set; }

    public string? Optiondescription { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual ICollection<Rewardtransaction> Rewardtransactions { get; set; } = new List<Rewardtransaction>();
}
