using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class User
{
    public Guid Userid { get; set; }

    public string? Fullname { get; set; }

    public string? Email { get; set; }

    public bool? Emailconfirmed { get; set; }

    public string Password { get; set; } = null!;

    public string? Status { get; set; }

    public string? Role { get; set; }

    public string? Avatar { get; set; }

    public DateOnly? Dob { get; set; }

    public string? Gender { get; set; }

    public string? Phonenumber { get; set; }

    public int? Rewardpoints { get; set; }

    public DateTime? Datecreated { get; set; }

    public DateTime? Datemodified { get; set; }

    public string? Refreshtoken { get; set; }

    public DateTime? Refreshtokenexpirytime { get; set; }

    public virtual ICollection<Address> Addresses { get; set; } = new List<Address>();

    public virtual ICollection<Conversation> ConversationUseroneNavigations { get; set; } = new List<Conversation>();

    public virtual ICollection<Conversation> ConversationUsertwoNavigations { get; set; } = new List<Conversation>();

    public virtual ICollection<Discountcodeuser> Discountcodeusers { get; set; } = new List<Discountcodeuser>();

    public virtual ICollection<Driverlocationhistory> Driverlocationhistories { get; set; } = new List<Driverlocationhistory>();

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Orderassignmenthistory> Orderassignmenthistories { get; set; } = new List<Orderassignmenthistory>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Orderstatushistory> Orderstatushistories { get; set; } = new List<Orderstatushistory>();

    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();

    public virtual ICollection<Rewardtransaction> Rewardtransactions { get; set; } = new List<Rewardtransaction>();
}
