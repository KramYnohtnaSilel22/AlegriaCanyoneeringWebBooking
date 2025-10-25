
using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AlegriaCanyoneeringWebBooking
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        public DbSet<Batch> Batches { get; set; }
        public DbSet<Guest> Guests { get; set; }
        public DbSet<OperatorList> OperatorLists { get; set; }
        public DbSet<GuestImage> GuestImage { get; set; }
        public DbSet<Nationality> Nationalities { get; set; }

        // //public DbSet<Driver> Drivers { get; set; }
        //public DbSet<Guide> Guides { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Operator> Operators { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Converter to handle int? <-> string
            var operatorIdConverter = new ValueConverter<int?, string>(
                v => v.HasValue ? v.Value.ToString() : null,            // Model -> DB
                v => string.IsNullOrEmpty(v) ? (int?)null : int.Parse(v) // DB -> Model
            );
            modelBuilder.Entity<Batch>(entity =>
            {




                // Apply conversion for OperatorId
                entity.Property(e => e.OperatorId)
                      .HasConversion(operatorIdConverter)
                      .HasColumnName("operatorname");

                //// Define relationship with Operator
                //entity.HasOne(e => e.Operators)
                //      .WithMany()
                //      .HasForeignKey(e => e.OperatorId)
                //      .HasPrincipalKey(o => o.Id);
            });
            modelBuilder.Entity<Guest>(entity =>
            {

             


                // Apply conversion for OperatorId
                entity.Property(e => e.OperatorId)
                      .HasConversion(operatorIdConverter)
                      .HasColumnName("operatorid");

                // Define relationship with Operator
                entity.HasOne(e => e.Operators)
                      .WithMany()
                      .HasForeignKey(e => e.OperatorId)
                      .HasPrincipalKey(o => o.Id);
            });
            modelBuilder.Entity<Guest>(entity =>
            {
                entity.ToTable("guest");

                // ====== Primary Key ======
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");

                // ====== Basic Properties ======
                entity.Property(e => e.Fullname)
                    .HasColumnName("fullname")
                    .HasMaxLength(1000);

                entity.Property(e => e.Age)
                    .HasColumnName("age")
                    .HasMaxLength(1000);



                entity.Property(e => e.Batch)
                    .HasColumnName("batch")
                    .HasMaxLength(100);

                entity.Property(e => e.NationalityType)
                    .HasColumnName("nationality")
                    .HasMaxLength(10000);

                entity.Property(e => e.NationalityId)
                    .HasColumnName("nat_stat");

                entity.Property(e => e.Gender)
                    .HasColumnName("gender")
                    .HasMaxLength(1000);

                entity.Property(e => e.Date)
                    .HasColumnName("date")
                    .HasMaxLength(1000);

                entity.Property(e => e.ArrivalDate)
                    .HasColumnName("arrivaldate")
                    .HasMaxLength(100);

                entity.Property(e => e.Month)
                    .HasColumnName("month");

                entity.Property(e => e.DateShort)
                    .HasColumnName("dateshort")
                    .HasMaxLength(100);


                entity.Property(e => e.Batch)
                      .HasColumnName("batchcode")
                      .HasMaxLength(100);

                entity.Property(e => e.Year)
                    .HasColumnName("year");

                entity.Property(e => e.BookingStatus)
                    .HasColumnName("status")
                     .HasDefaultValue(0); // Set default value to 0 (representing "anticipated")

                entity.Property(e => e.RFID)
    .HasColumnName("rfid");

                entity.Property(e => e.RFIDCode)
                    .HasColumnName("rfidcode")
                    .HasMaxLength(1000);


                entity.Property(e => e.Area)
                    .HasColumnName("Area")
                    .HasMaxLength(500);

                entity.Property(e => e.ContactNumber)
                    .HasColumnName("ContactNum")
                    .HasMaxLength(500);

                entity.Property(e => e.OperatorId)
                    .HasColumnName("operatorid");

                // ====== Relationships ======

                entity.HasOne(g => g.OperatorList)
                    .WithMany(o => o.Guests)
                    .HasForeignKey(g => g.OperatorId)
                    .HasConstraintName("FK_Guest_Operator")
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);

                entity.HasOne(g => g.NationalityEntity)
                    .WithMany()
                    .HasForeignKey(g => g.NationalityId)
                    .HasConstraintName("FK_Guest_Nationality")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);
                modelBuilder.Entity<Guest>()
        .HasOne(g => g.Operators)
        .WithMany(o => o.Guests)
        .HasForeignKey(g => g.OperatorId)
        .OnDelete(DeleteBehavior.Restrict); // Or Cascade, if you prefer
                // ====== Indexes ======
                entity.HasIndex(e => e.Fullname).HasDatabaseName("IX_guests_fullname");
                entity.HasIndex(e => e.RFID).HasDatabaseName("IX_guests_rfid");
                entity.HasIndex(e => e.NationalityType).HasDatabaseName("IX_guests_nationality");
                entity.HasIndex(e => e.NationalityId).HasDatabaseName("IX_guests_nationality_id");
                entity.HasIndex(e => e.BookingStatus).HasDatabaseName("IX_guests_booking_status");
            });

            // OperatorList entity configuration
            modelBuilder.Entity<OperatorList>(entity =>
            {
                entity.ToTable("operator_list");
                entity.HasKey(e => e.OperatorId);

                entity.Property(e => e.OperatorId).HasColumnName("id");
                entity.Property(e => e.OwnerName).HasColumnName("owner_name").HasMaxLength(1000).IsRequired();
                entity.Property(e => e.Gender).HasColumnName("gender").HasMaxLength(1000).IsRequired();
                entity.Property(e => e.BusinessName).HasColumnName("business_name").HasMaxLength(1000).IsRequired();
                entity.Property(e => e.BussPermit).HasColumnName("buss_permit").HasMaxLength(1000).IsRequired();
                entity.Property(e => e.Location).HasColumnName("location").HasMaxLength(1000).IsRequired();
                entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            });


            modelBuilder.Entity<Nationality>().ToTable("nationalities");
            modelBuilder.Entity<Batch>().ToTable("tblbatch");


            // Removed Driver entity configuration
            // modelBuilder.Entity<Driver>().ToTable("driver_details");
            //modelBuilder.Entity<Guide>().ToTable("tourguide_details");
            modelBuilder.Entity<Role>().ToTable("role");
            modelBuilder.Entity<Operator>().ToTable("tbl_operator_mobile");

        }
    }
}
