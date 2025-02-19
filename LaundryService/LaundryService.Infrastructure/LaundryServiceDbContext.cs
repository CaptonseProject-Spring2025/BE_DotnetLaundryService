using System;
using System.Collections.Generic;
using LaundryService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LaundryService.Infrastructure;

public partial class LaundryServiceDbContext : DbContext
{
    //public LaundryServiceDbContext()
    //{
    //}

    public LaundryServiceDbContext(DbContextOptions<LaundryServiceDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Address> Addresses { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<Discountcode> Discountcodes { get; set; }

    public virtual DbSet<Driverlocationhistory> Driverlocationhistories { get; set; }

    public virtual DbSet<Extra> Extras { get; set; }

    public virtual DbSet<Extracategory> Extracategories { get; set; }

    public virtual DbSet<Membershiptier> Membershiptiers { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<Orderdiscount> Orderdiscounts { get; set; }

    public virtual DbSet<Orderdriver> Orderdrivers { get; set; }

    public virtual DbSet<Orderextra> Orderextras { get; set; }

    public virtual DbSet<Orderitem> Orderitems { get; set; }

    public virtual DbSet<Orderphoto> Orderphotos { get; set; }

    public virtual DbSet<Orderstatushistory> Orderstatushistories { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Paymentmethod> Paymentmethods { get; set; }

    public virtual DbSet<Rating> Ratings { get; set; }

    public virtual DbSet<Servicecategory> Servicecategories { get; set; }

    public virtual DbSet<Servicedetail> Servicedetails { get; set; }

    public virtual DbSet<Serviceextramapping> Serviceextramappings { get; set; }

    public virtual DbSet<Subservice> Subservices { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Usermembershipstatus> Usermembershipstatuses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<Address>(entity =>
        {
            entity.HasKey(e => e.Addressid).HasName("addresses_pkey");

            entity.ToTable("addresses");

            entity.Property(e => e.Addressid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("addressid");
            entity.Property(e => e.Addresslabel)
                .HasDefaultValueSql("'Home'::text")
                .HasColumnName("addresslabel");
            entity.Property(e => e.Contactname).HasColumnName("contactname");
            entity.Property(e => e.Contactphone).HasColumnName("contactphone");
            entity.Property(e => e.Datecreated)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("datecreated");
            entity.Property(e => e.Datemodified)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("datemodified");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Detailaddress).HasColumnName("detailaddress");
            entity.Property(e => e.Isdefault)
                .HasDefaultValue(false)
                .HasColumnName("isdefault");
            entity.Property(e => e.Latitude)
                .HasPrecision(9, 6)
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasPrecision(9, 6)
                .HasColumnName("longitude");
            entity.Property(e => e.Userid).HasColumnName("userid");

            entity.HasOne(d => d.User).WithMany(p => p.Addresses)
                .HasForeignKey(d => d.Userid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("addresses_userid_fkey");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Conversationid).HasName("conversation_pkey");

            entity.ToTable("conversation");

            entity.Property(e => e.Conversationid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("conversationid");
            entity.Property(e => e.Creationdate)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("creationdate");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Userone).HasColumnName("userone");
            entity.Property(e => e.Usertwo).HasColumnName("usertwo");

            entity.HasOne(d => d.UseroneNavigation).WithMany(p => p.ConversationUseroneNavigations)
                .HasForeignKey(d => d.Userone)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("conversation_userone_fkey");

            entity.HasOne(d => d.UsertwoNavigation).WithMany(p => p.ConversationUsertwoNavigations)
                .HasForeignKey(d => d.Usertwo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("conversation_usertwo_fkey");
        });

        modelBuilder.Entity<Discountcode>(entity =>
        {
            entity.HasKey(e => e.Discountcodeid).HasName("discountcodes_pkey");

            entity.ToTable("discountcodes");

            entity.HasIndex(e => e.Code, "discountcodes_code_key").IsUnique();

            entity.Property(e => e.Discountcodeid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("discountcodeid");
            entity.Property(e => e.Appliesto).HasColumnName("appliesto");
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Discounttype).HasColumnName("discounttype");
            entity.Property(e => e.Enddate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("enddate");
            entity.Property(e => e.Maximumdiscount)
                .HasPrecision(10)
                .HasColumnName("maximumdiscount");
            entity.Property(e => e.Minimumordervalue)
                .HasPrecision(10)
                .HasColumnName("minimumordervalue");
            entity.Property(e => e.Startdate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("startdate");
            entity.Property(e => e.Usagelimit).HasColumnName("usagelimit");
            entity.Property(e => e.Usageperuser).HasColumnName("usageperuser");
            entity.Property(e => e.Value)
                .HasPrecision(10)
                .HasColumnName("value");
        });

        modelBuilder.Entity<Driverlocationhistory>(entity =>
        {
            entity.HasKey(e => e.Historyid).HasName("driverlocationhistory_pkey");

            entity.ToTable("driverlocationhistory");

            entity.Property(e => e.Historyid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("historyid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Driverid).HasColumnName("driverid");
            entity.Property(e => e.Latitude)
                .HasPrecision(9, 6)
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasPrecision(9, 6)
                .HasColumnName("longitude");
            entity.Property(e => e.Orderid).HasColumnName("orderid");

            entity.HasOne(d => d.Driver).WithMany(p => p.Driverlocationhistories)
                .HasForeignKey(d => d.Driverid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("driverlocationhistory_driverid_fkey");

            entity.HasOne(d => d.Order).WithMany(p => p.Driverlocationhistories)
                .HasForeignKey(d => d.Orderid)
                .HasConstraintName("driverlocationhistory_orderid_fkey");
        });

        modelBuilder.Entity<Extra>(entity =>
        {
            entity.HasKey(e => e.Extraid).HasName("extras_pkey");

            entity.ToTable("extras");

            entity.Property(e => e.Extraid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("extraid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Extracategoryid).HasColumnName("extracategoryid");
            entity.Property(e => e.Image).HasColumnName("image");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Price)
                .HasPrecision(10)
                .HasColumnName("price");

            entity.HasOne(d => d.Extracategory).WithMany(p => p.Extras)
                .HasForeignKey(d => d.Extracategoryid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("extras_extracategoryid_fkey");
        });

        modelBuilder.Entity<Extracategory>(entity =>
        {
            entity.HasKey(e => e.Extracategoryid).HasName("extracategories_pkey");

            entity.ToTable("extracategories");

            entity.HasIndex(e => e.Name, "extracategories_name_key").IsUnique();

            entity.Property(e => e.Extracategoryid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("extracategoryid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<Membershiptier>(entity =>
        {
            entity.HasKey(e => e.Tierid).HasName("membershiptiers_pkey");

            entity.ToTable("membershiptiers");

            entity.Property(e => e.Tierid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("tierid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Isactive)
                .HasDefaultValue(true)
                .HasColumnName("isactive");
            entity.Property(e => e.Lowerbound)
                .HasPrecision(10)
                .HasColumnName("lowerbound");
            entity.Property(e => e.Tiername).HasColumnName("tiername");
            entity.Property(e => e.Updatedat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updatedat");
            entity.Property(e => e.Upperbound)
                .HasPrecision(10)
                .HasColumnName("upperbound");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Messageid).HasName("message_pkey");

            entity.ToTable("message");

            entity.Property(e => e.Messageid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("messageid");
            entity.Property(e => e.Conversationid).HasColumnName("conversationid");
            entity.Property(e => e.Creationdate)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("creationdate");
            entity.Property(e => e.Imagelink).HasColumnName("imagelink");
            entity.Property(e => e.Isseen)
                .HasDefaultValue(false)
                .HasColumnName("isseen");
            entity.Property(e => e.Issent)
                .HasDefaultValue(true)
                .HasColumnName("issent");
            entity.Property(e => e.Message1).HasColumnName("message");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Typeis).HasColumnName("typeis");
            entity.Property(e => e.Updatedate)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updatedate");
            entity.Property(e => e.Userid).HasColumnName("userid");

            entity.HasOne(d => d.Conversation).WithMany(p => p.Messages)
                .HasForeignKey(d => d.Conversationid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("message_conversationid_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Messages)
                .HasForeignKey(d => d.Userid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("message_userid_fkey");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Notificationid).HasName("notifications_pkey");

            entity.ToTable("notifications");

            entity.Property(e => e.Notificationid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("notificationid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Ispushenabled).HasColumnName("ispushenabled");
            entity.Property(e => e.Isread)
                .HasDefaultValue(false)
                .HasColumnName("isread");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.Notificationtype).HasColumnName("notificationtype");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.Userid).HasColumnName("userid");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.Userid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("notifications_userid_fkey");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Orderid).HasName("orders_pkey");

            entity.ToTable("orders");

            entity.Property(e => e.Orderid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("orderid");
            entity.Property(e => e.Applicablefee)
                .HasPrecision(10)
                .HasColumnName("applicablefee");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Currentstatus).HasColumnName("currentstatus");
            entity.Property(e => e.Deliveryaddressdetail).HasColumnName("deliveryaddressdetail");
            entity.Property(e => e.Deliverydescription).HasColumnName("deliverydescription");
            entity.Property(e => e.Deliverylabel).HasColumnName("deliverylabel");
            entity.Property(e => e.Deliverylatitude)
                .HasPrecision(9, 6)
                .HasColumnName("deliverylatitude");
            entity.Property(e => e.Deliverylongitude)
                .HasPrecision(9, 6)
                .HasColumnName("deliverylongitude");
            entity.Property(e => e.Deliveryname).HasColumnName("deliveryname");
            entity.Property(e => e.Deliveryphone).HasColumnName("deliveryphone");
            entity.Property(e => e.Deliverytime)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("deliverytime");
            entity.Property(e => e.Pickupaddressdetail).HasColumnName("pickupaddressdetail");
            entity.Property(e => e.Pickupdescription).HasColumnName("pickupdescription");
            entity.Property(e => e.Pickuplabel).HasColumnName("pickuplabel");
            entity.Property(e => e.Pickuplatitude)
                .HasPrecision(9, 6)
                .HasColumnName("pickuplatitude");
            entity.Property(e => e.Pickuplongitude)
                .HasPrecision(9, 6)
                .HasColumnName("pickuplongitude");
            entity.Property(e => e.Pickupname).HasColumnName("pickupname");
            entity.Property(e => e.Pickupphone).HasColumnName("pickupphone");
            entity.Property(e => e.Pickuptime)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("pickuptime");
            entity.Property(e => e.Shippingfee)
                .HasPrecision(10)
                .HasColumnName("shippingfee");
            entity.Property(e => e.Totalprice)
                .HasPrecision(10)
                .HasColumnName("totalprice");
            entity.Property(e => e.Userid).HasColumnName("userid");

            entity.HasOne(d => d.User).WithMany(p => p.Orders)
                .HasForeignKey(d => d.Userid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orders_userid_fkey");
        });

        modelBuilder.Entity<Orderdiscount>(entity =>
        {
            entity.HasKey(e => e.Orderdiscountid).HasName("orderdiscounts_pkey");

            entity.ToTable("orderdiscounts");

            entity.Property(e => e.Orderdiscountid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("orderdiscountid");
            entity.Property(e => e.Appliedat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("appliedat");
            entity.Property(e => e.Discountamount)
                .HasPrecision(10)
                .HasColumnName("discountamount");
            entity.Property(e => e.Discountcodeid).HasColumnName("discountcodeid");
            entity.Property(e => e.Orderid).HasColumnName("orderid");

            entity.HasOne(d => d.Discountcode).WithMany(p => p.Orderdiscounts)
                .HasForeignKey(d => d.Discountcodeid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orderdiscounts_discountcodeid_fkey");

            entity.HasOne(d => d.Order).WithMany(p => p.Orderdiscounts)
                .HasForeignKey(d => d.Orderid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orderdiscounts_orderid_fkey");
        });

        modelBuilder.Entity<Orderdriver>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("orderdrivers_pkey");

            entity.ToTable("orderdrivers");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Assignedat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("assignedat");
            entity.Property(e => e.Driverid).HasColumnName("driverid");
            entity.Property(e => e.Orderid).HasColumnName("orderid");
            entity.Property(e => e.Role).HasColumnName("role");

            entity.HasOne(d => d.Driver).WithMany(p => p.Orderdrivers)
                .HasForeignKey(d => d.Driverid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orderdrivers_driverid_fkey");

            entity.HasOne(d => d.Order).WithMany(p => p.Orderdrivers)
                .HasForeignKey(d => d.Orderid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orderdrivers_orderid_fkey");
        });

        modelBuilder.Entity<Orderextra>(entity =>
        {
            entity.HasKey(e => e.Orderextraid).HasName("orderextras_pkey");

            entity.ToTable("orderextras");

            entity.Property(e => e.Orderextraid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("orderextraid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Extraid).HasColumnName("extraid");
            entity.Property(e => e.Extraprice)
                .HasPrecision(10)
                .HasColumnName("extraprice");
            entity.Property(e => e.Orderitemid).HasColumnName("orderitemid");

            entity.HasOne(d => d.Extra).WithMany(p => p.Orderextras)
                .HasForeignKey(d => d.Extraid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orderextras_extraid_fkey");

            entity.HasOne(d => d.Orderitem).WithMany(p => p.Orderextras)
                .HasForeignKey(d => d.Orderitemid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orderextras_orderitemid_fkey");
        });

        modelBuilder.Entity<Orderitem>(entity =>
        {
            entity.HasKey(e => e.Orderitemid).HasName("orderitems_pkey");

            entity.ToTable("orderitems");

            entity.Property(e => e.Orderitemid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("orderitemid");
            entity.Property(e => e.Baseprice)
                .HasPrecision(10)
                .HasColumnName("baseprice");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Orderid).HasColumnName("orderid");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.Serviceid).HasColumnName("serviceid");

            entity.HasOne(d => d.Order).WithMany(p => p.Orderitems)
                .HasForeignKey(d => d.Orderid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orderitems_orderid_fkey");

            entity.HasOne(d => d.Service).WithMany(p => p.Orderitems)
                .HasForeignKey(d => d.Serviceid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orderitems_serviceid_fkey");
        });

        modelBuilder.Entity<Orderphoto>(entity =>
        {
            entity.HasKey(e => e.Photoid).HasName("orderphotos_pkey");

            entity.ToTable("orderphotos");

            entity.Property(e => e.Photoid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("photoid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Driverid).HasColumnName("driverid");
            entity.Property(e => e.Orderid).HasColumnName("orderid");
            entity.Property(e => e.Photosequence).HasColumnName("photosequence");
            entity.Property(e => e.Phototype).HasColumnName("phototype");
            entity.Property(e => e.Photourl).HasColumnName("photourl");

            entity.HasOne(d => d.Driver).WithMany(p => p.Orderphotos)
                .HasForeignKey(d => d.Driverid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orderphotos_driverid_fkey");

            entity.HasOne(d => d.Order).WithMany(p => p.Orderphotos)
                .HasForeignKey(d => d.Orderid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orderphotos_orderid_fkey");
        });

        modelBuilder.Entity<Orderstatushistory>(entity =>
        {
            entity.HasKey(e => e.Statushistoryid).HasName("orderstatushistory_pkey");

            entity.ToTable("orderstatushistory");

            entity.Property(e => e.Statushistoryid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("statushistoryid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Orderid).HasColumnName("orderid");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Statusdescription).HasColumnName("statusdescription");
            entity.Property(e => e.Updatedby).HasColumnName("updatedby");

            entity.HasOne(d => d.Order).WithMany(p => p.Orderstatushistories)
                .HasForeignKey(d => d.Orderid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orderstatushistory_orderid_fkey");

            entity.HasOne(d => d.UpdatedbyNavigation).WithMany(p => p.Orderstatushistories)
                .HasForeignKey(d => d.Updatedby)
                .HasConstraintName("orderstatushistory_updatedby_fkey");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Paymentid).HasName("payments_pkey");

            entity.ToTable("payments");

            entity.Property(e => e.Paymentid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("paymentid");
            entity.Property(e => e.Amount)
                .HasPrecision(10)
                .HasColumnName("amount");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Orderid).HasColumnName("orderid");
            entity.Property(e => e.Paymentdate)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("paymentdate");
            entity.Property(e => e.Paymentmetadata)
                .HasColumnType("jsonb")
                .HasColumnName("paymentmetadata");
            entity.Property(e => e.Paymentmethodid).HasColumnName("paymentmethodid");
            entity.Property(e => e.Paymentstatus).HasColumnName("paymentstatus");
            entity.Property(e => e.Transactionid).HasColumnName("transactionid");
            entity.Property(e => e.Updatedat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updatedat");

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.Orderid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("payments_orderid_fkey");

            entity.HasOne(d => d.Paymentmethod).WithMany(p => p.Payments)
                .HasForeignKey(d => d.Paymentmethodid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("payments_paymentmethodid_fkey");
        });

        modelBuilder.Entity<Paymentmethod>(entity =>
        {
            entity.HasKey(e => e.Paymentmethodid).HasName("paymentmethods_pkey");

            entity.ToTable("paymentmethods");

            entity.HasIndex(e => e.Name, "paymentmethods_name_key").IsUnique();

            entity.Property(e => e.Paymentmethodid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("paymentmethodid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Isactive)
                .HasDefaultValue(true)
                .HasColumnName("isactive");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<Rating>(entity =>
        {
            entity.HasKey(e => e.Ratingid).HasName("ratings_pkey");

            entity.ToTable("ratings");

            entity.Property(e => e.Ratingid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("ratingid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Orderid).HasColumnName("orderid");
            entity.Property(e => e.Rating1).HasColumnName("rating");
            entity.Property(e => e.Review).HasColumnName("review");
            entity.Property(e => e.Serviceid).HasColumnName("serviceid");
            entity.Property(e => e.Userid).HasColumnName("userid");

            entity.HasOne(d => d.Order).WithMany(p => p.Ratings)
                .HasForeignKey(d => d.Orderid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ratings_orderid_fkey");

            entity.HasOne(d => d.Service).WithMany(p => p.Ratings)
                .HasForeignKey(d => d.Serviceid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ratings_serviceid_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Ratings)
                .HasForeignKey(d => d.Userid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ratings_userid_fkey");
        });

        modelBuilder.Entity<Servicecategory>(entity =>
        {
            entity.HasKey(e => e.Categoryid).HasName("servicecategories_pkey");

            entity.ToTable("servicecategories");

            entity.HasIndex(e => e.Name, "servicecategories_name_key").IsUnique();

            entity.Property(e => e.Categoryid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("categoryid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Icon).HasColumnName("icon");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<Servicedetail>(entity =>
        {
            entity.HasKey(e => e.Serviceid).HasName("servicedetails_pkey");

            entity.ToTable("servicedetails");

            entity.Property(e => e.Serviceid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("serviceid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Image).HasColumnName("image");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Price)
                .HasPrecision(10)
                .HasColumnName("price");
            entity.Property(e => e.Subserviceid).HasColumnName("subserviceid");

            entity.HasOne(d => d.Subservice).WithMany(p => p.Servicedetails)
                .HasForeignKey(d => d.Subserviceid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("servicedetails_subserviceid_fkey");
        });

        modelBuilder.Entity<Serviceextramapping>(entity =>
        {
            entity.HasKey(e => e.Mappingid).HasName("serviceextramapping_pkey");

            entity.ToTable("serviceextramapping");

            entity.Property(e => e.Mappingid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("mappingid");
            entity.Property(e => e.Extraid).HasColumnName("extraid");
            entity.Property(e => e.Serviceid).HasColumnName("serviceid");

            entity.HasOne(d => d.Extra).WithMany(p => p.Serviceextramappings)
                .HasForeignKey(d => d.Extraid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("serviceextramapping_extraid_fkey");

            entity.HasOne(d => d.Service).WithMany(p => p.Serviceextramappings)
                .HasForeignKey(d => d.Serviceid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("serviceextramapping_serviceid_fkey");
        });

        modelBuilder.Entity<Subservice>(entity =>
        {
            entity.HasKey(e => e.Subserviceid).HasName("subservices_pkey");

            entity.ToTable("subservices");

            entity.Property(e => e.Subserviceid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("subserviceid");
            entity.Property(e => e.Categoryid).HasColumnName("categoryid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name).HasColumnName("name");

            entity.HasOne(d => d.Category).WithMany(p => p.Subservices)
                .HasForeignKey(d => d.Categoryid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("subservices_categoryid_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Userid).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.HasIndex(e => e.Phonenumber, "users_phonenumber_key").IsUnique();

            entity.HasIndex(e => e.Username, "users_username_key").IsUnique();

            entity.Property(e => e.Userid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("userid");
            entity.Property(e => e.Avatar).HasColumnName("avatar");
            entity.Property(e => e.Datecreated)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("datecreated");
            entity.Property(e => e.Datemodified)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("datemodified");
            entity.Property(e => e.Dob).HasColumnName("dob");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Emailconfirmed)
                .HasDefaultValue(false)
                .HasColumnName("emailconfirmed");
            entity.Property(e => e.Fullname).HasColumnName("fullname");
            entity.Property(e => e.Gender).HasColumnName("gender");
            entity.Property(e => e.Password).HasColumnName("password");
            entity.Property(e => e.Phonenumber).HasColumnName("phonenumber");
            entity.Property(e => e.Phonenumberconfirmed)
                .HasDefaultValue(false)
                .HasColumnName("phonenumberconfirmed");
            entity.Property(e => e.Refreshtoken).HasColumnName("refreshtoken");
            entity.Property(e => e.Refreshtokenexpirytime)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("refreshtokenexpirytime");
            entity.Property(e => e.Role)
                .HasDefaultValueSql("'CUSTOMER'::text")
                .HasColumnName("role");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'ACTIVE'::text")
                .HasColumnName("status");
            entity.Property(e => e.Username).HasColumnName("username");
        });

        modelBuilder.Entity<Usermembershipstatus>(entity =>
        {
            entity.HasKey(e => e.Usermembershipid).HasName("usermembershipstatus_pkey");

            entity.ToTable("usermembershipstatus");

            entity.Property(e => e.Usermembershipid)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("usermembershipid");
            entity.Property(e => e.Lastupdated)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("lastupdated");
            entity.Property(e => e.Tierid).HasColumnName("tierid");
            entity.Property(e => e.Totalspending)
                .HasPrecision(10)
                .HasColumnName("totalspending");
            entity.Property(e => e.Userid).HasColumnName("userid");

            entity.HasOne(d => d.Tier).WithMany(p => p.Usermembershipstatuses)
                .HasForeignKey(d => d.Tierid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("usermembershipstatus_tierid_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Usermembershipstatuses)
                .HasForeignKey(d => d.Userid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("usermembershipstatus_userid_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
