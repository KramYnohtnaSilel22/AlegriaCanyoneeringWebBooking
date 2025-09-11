using Microsoft.EntityFrameworkCore;

namespace AlegriaCanyoneeringWebBooking.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        public DbSet<Batch> Batches { get; set; }
        public DbSet<Guest> Guests { get; set; }
        public DbSet<Operator> Operators { get; set; }
        public DbSet<Reserve> Reserves { get; set; }

        public DbSet<Nationality> Nationalities { get; set; }

        public DbSet<Driver> Drivers { get; set; }

        public DbSet<Guide> Guides { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Guest entity configuration
            // ===========================
            modelBuilder.Entity<Guest>(entity =>
            {
                entity.ToTable("guest");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Fullname).HasColumnName("fullname").HasMaxLength(1000).IsRequired();
                entity.Property(e => e.Age).HasColumnName("age").HasMaxLength(1000).IsRequired();
                entity.Property(e => e.NationalityType).HasColumnName("nationality").HasMaxLength(10000).IsRequired(false); // Changed to not required

                // CRITICAL FIX: Make these nullable as they can be NULL in database
                entity.Property(e => e.NationalityId).HasColumnName("natstat").IsRequired(false); // Changed from IsRequired()
                entity.Property(e => e.OperatorId).HasColumnName("operatorid").IsRequired(false); // Changed from IsRequired()
                entity.Property(e => e.DriverId).HasColumnName("driverid").IsRequired(false); // Changed from IsRequired()
                entity.Property(e => e.GuideId).HasColumnName("guideid").IsRequired(false); // Changed from IsRequired()

                entity.Property(e => e.Gender).HasColumnName("gender").HasMaxLength(1000).IsRequired();
                entity.Property(e => e.Date).HasColumnName("date").HasMaxLength(1000).IsRequired();
                entity.Property(e => e.ArrivalDate).HasColumnName("arrivaldate").HasMaxLength(100).IsRequired();
                entity.Property(e => e.Month).HasColumnName("month").HasMaxLength(500).IsRequired();

                // Make these nullable as they can be NULL
                entity.Property(e => e.DateShort).HasColumnName("dateshort").HasMaxLength(100).IsRequired(false); // Changed from IsRequired()
                entity.Property(e => e.RFID).HasColumnName("rfid").IsRequired(false); // Changed from IsRequired() and fixed column name
                entity.Property(e => e.BookingStatus).HasColumnName("bookingstatus").HasMaxLength(50).HasDefaultValue("anticipated"); // Fixed column name
                entity.Property(e => e.QrCode).HasColumnName("qrcode").HasMaxLength(500); // Fixed column name

                entity.Property(e => e.NumberOfGuests).HasColumnName("number_of_guests").IsRequired();
                entity.Property(e => e.Area).HasColumnName("Area").HasMaxLength(500).IsRequired();
                entity.Property(e => e.ContactNumber).HasColumnName("ContactNum").HasMaxLength(500).IsRequired();
                entity.Property(e => e.Batch).HasColumnName("batch").HasMaxLength(100);

                // Configure foreign key relationships with proper cascade behavior for nullable FKs
                entity.HasOne(g => g.Operator)
                      .WithMany(o => o.Guests)
                      .HasForeignKey(g => g.OperatorId)
                      .HasConstraintName("FK_Guest_Operator")
                      .OnDelete(DeleteBehavior.SetNull) // Changed to SetNull for nullable FK
                      .IsRequired(false); // Make the relationship optional

                entity.HasOne(g => g.Driver)
                      .WithMany(d => d.Guests)
                      .HasForeignKey(g => g.DriverId)
                      .HasConstraintName("FK_Guest_Driver")
                      .OnDelete(DeleteBehavior.SetNull) // Changed to SetNull for nullable FK
                      .IsRequired(false); // Make the relationship optional

                entity.HasOne(g => g.Guide)
                      .WithMany(gd => gd.Guests)
                      .HasForeignKey(g => g.GuideId)
                      .HasConstraintName("FK_Guest_Guide")
                      .OnDelete(DeleteBehavior.SetNull) // Changed to SetNull for nullable FK
                      .IsRequired(false); // Make the relationship optional

                entity.HasOne(g => g.Nationality)
                      .WithMany()
                      .HasForeignKey(g => g.NationalityId)
                      .HasConstraintName("FK_Guest_Nationality")
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired(false); // Make the relationship optional

                entity.HasIndex(e => e.Fullname).HasDatabaseName("IX_guests_fullname");
                entity.HasIndex(e => e.RFID).HasDatabaseName("IX_guests_rfid");
                entity.HasIndex(e => e.NationalityType).HasDatabaseName("IX_guests_nationality");
                entity.HasIndex(e => e.NationalityId).HasDatabaseName("IX_guests_nationality_id");
                entity.HasIndex(e => e.BookingStatus).HasDatabaseName("IX_guests_booking_status");
            });

            // OperatorList entity configuration
            modelBuilder.Entity<Operator>(entity =>
            {
                entity.ToTable("operator_list");
                entity.HasKey(e => e.OperatorId);

                // Column mappings
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
            modelBuilder.Entity<Driver>().ToTable("driver_details");
            modelBuilder.Entity<Guide>().ToTable("tourguide_details");
        }
    }
}