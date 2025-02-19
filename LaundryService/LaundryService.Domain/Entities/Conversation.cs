using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Conversation
{
    public Guid Conversationid { get; set; }

    public Guid Userone { get; set; }

    public Guid Usertwo { get; set; }

    public DateTime? Creationdate { get; set; }

    public string? Status { get; set; }

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual User UseroneNavigation { get; set; } = null!;

    public virtual User UsertwoNavigation { get; set; } = null!;
}
