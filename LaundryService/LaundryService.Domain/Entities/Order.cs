using System;
using System.Collections.Generic;

namespace LaundryService.Domain.Entities;

public partial class Order
{
    public Guid Orderid { get; set; }

    public Guid Userid { get; set; }

    public string Pickuplabel { get; set; } = null!;

    public string Pickupname { get; set; } = null!;

    public string Pickupphone { get; set; } = null!;

    public string Pickupaddressdetail { get; set; } = null!;

    public string? Pickupdescription { get; set; }

    public decimal? Pickuplatitude { get; set; }

    public decimal? Pickuplongitude { get; set; }

    public string Deliverylabel { get; set; } = null!;

    public string Deliveryname { get; set; } = null!;

    public string Deliveryphone { get; set; } = null!;

    public string Deliveryaddressdetail { get; set; } = null!;

    public string? Deliverydescription { get; set; }

    public decimal? Deliverylatitude { get; set; }

    public decimal? Deliverylongitude { get; set; }

    public DateTime Pickuptime { get; set; }

    public DateTime Deliverytime { get; set; }

    public decimal? Shippingfee { get; set; }

    public decimal? Applicablefee { get; set; }

    public decimal? Totalprice { get; set; }

    public string? Currentstatus { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual ICollection<Driverlocationhistory> Driverlocationhistories { get; set; } = new List<Driverlocationhistory>();

    public virtual ICollection<Orderdiscount> Orderdiscounts { get; set; } = new List<Orderdiscount>();

    public virtual ICollection<Orderdriver> Orderdrivers { get; set; } = new List<Orderdriver>();

    public virtual ICollection<Orderitem> Orderitems { get; set; } = new List<Orderitem>();

    public virtual ICollection<Orderphoto> Orderphotos { get; set; } = new List<Orderphoto>();

    public virtual ICollection<Orderstatushistory> Orderstatushistories { get; set; } = new List<Orderstatushistory>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();

    public virtual User User { get; set; } = null!;
}
