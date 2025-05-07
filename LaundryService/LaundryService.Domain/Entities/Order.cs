using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Order
{
    public string Orderid { get; set; } = null!;

    public Guid Userid { get; set; }

    public string? Pickuplabel { get; set; }

    public string? Pickupname { get; set; }

    public string? Pickupphone { get; set; }

    public string? Pickupaddressdetail { get; set; }

    public string? Pickupdescription { get; set; }

    public decimal? Pickuplatitude { get; set; }

    public decimal? Pickuplongitude { get; set; }

    public string? Deliverylabel { get; set; }

    public string? Deliveryname { get; set; }

    public string? Deliveryphone { get; set; }

    public string? Deliveryaddressdetail { get; set; }

    public string? Deliverydescription { get; set; }

    public decimal? Deliverylatitude { get; set; }

    public decimal? Deliverylongitude { get; set; }

    public DateTime? Pickuptime { get; set; }

    public DateTime? Deliverytime { get; set; }

    public decimal? Shippingfee { get; set; }

    public decimal? Shippingdiscount { get; set; }

    public decimal? Applicablefee { get; set; }

    public decimal? Otherprice { get; set; }

    public string? Noteforotherprice { get; set; }

    public decimal? Totalprice { get; set; }

    public decimal? Discount { get; set; }

    public string? Currentstatus { get; set; }

    public DateTime? Createdat { get; set; }

    public bool? Emergency { get; set; }

    public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

    public virtual ICollection<Driverlocationhistory> Driverlocationhistories { get; set; } = new List<Driverlocationhistory>();

    public virtual ICollection<Orderassignmenthistory> Orderassignmenthistories { get; set; } = new List<Orderassignmenthistory>();

    public virtual ICollection<Orderdiscount> Orderdiscounts { get; set; } = new List<Orderdiscount>();

    public virtual ICollection<Orderitem> Orderitems { get; set; } = new List<Orderitem>();

    public virtual ICollection<Orderstatushistory> Orderstatushistories { get; set; } = new List<Orderstatushistory>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();

    public virtual User User { get; set; } = null!;
}
