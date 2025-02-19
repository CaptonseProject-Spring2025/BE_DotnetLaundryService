using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Message
{
    public Guid Messageid { get; set; }

    public Guid Conversationid { get; set; }

    public Guid Userid { get; set; }

    public string? Message1 { get; set; }

    public string? Typeis { get; set; }

    public string? Imagelink { get; set; }

    public DateTime? Creationdate { get; set; }

    public DateTime? Updatedate { get; set; }

    public bool? Issent { get; set; }

    public bool? Isseen { get; set; }

    public string? Status { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
