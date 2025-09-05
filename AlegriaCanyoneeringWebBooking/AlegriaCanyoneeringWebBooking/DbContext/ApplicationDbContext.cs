using Microsoft.EntityFrameworkCore;

namespace AlegriaCanyoneeringWebBooking.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        public DbSet<Guest> Guests { get; set; }
        public DbSet<Operator> Operators { get; set; }
        public DbSet<Reserve> Reserves { get; set; }

        public DbSet<Nationality> Nationalities { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===========================
            // Guest entity configuration
            // Guest entity configuration
            // ===========================
            modelBuilder.Entity<Guest>(entity =>
            {
                entity.ToTable("guest");
                entity.HasKey(e => e.Id);

                // Column mappings
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Fullname).HasColumnName("fullname").HasMaxLength(1000).IsRequired();
                entity.Property(e => e.Age).HasColumnName("age").HasMaxLength(1000).IsRequired();
                entity.Property(e => e.NationalityType).HasColumnName("nationality").HasMaxLength(10000).IsRequired();
                entity.Property(e => e.NationalityId).HasColumnName("nat_stat").IsRequired();
                entity.Property(e => e.OperatorId).HasColumnName("operatorid").IsRequired();
                entity.Property(e => e.Gender).HasColumnName("gender").HasMaxLength(1000).IsRequired();
                entity.Property(e => e.Date).HasColumnName("date").HasMaxLength(1000).IsRequired();
                entity.Property(e => e.ArrivalDate).HasColumnName("arrivaldate").HasMaxLength(100).IsRequired();
                entity.Property(e => e.Month).HasColumnName("month").HasMaxLength(500).IsRequired();
                entity.Property(e => e.DateShort).HasColumnName("dateshort").HasMaxLength(100).IsRequired();
                entity.Property(e => e.RFID).HasColumnName("RFID").IsRequired();
                entity.Property(e => e.BookingStatus).HasColumnName("BookingStatus").HasMaxLength(50).HasDefaultValue("anticipated");
                entity.Property(e => e.QrCode).HasColumnName("QrCode").HasMaxLength(500);
                entity.Property(e => e.NumberOfGuests).HasColumnName("number_of_guests").IsRequired();
                entity.Property(e => e.Batch).HasColumnName("batch").HasMaxLength(100);

                // Foreign key relationships
                entity.HasOne(g => g.Operator)
                      .WithMany(o => o.Guests)
                      .HasForeignKey(g => g.OperatorId)
                      .HasConstraintName("FK_Guest_Operator")
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(g => g.Nationality)
                      .WithMany()
                      .HasForeignKey(g => g.NationalityId)
                      .HasConstraintName("FK_Guest_Nationality")
                      .OnDelete(DeleteBehavior.Restrict);

                // Indexes - Fix: Use actual column properties, not navigation properties
                entity.HasIndex(e => e.Fullname).HasDatabaseName("IX_guests_fullname");
                entity.HasIndex(e => e.RFID).HasDatabaseName("IX_guests_rfid");
                entity.HasIndex(e => e.NationalityType).HasDatabaseName("IX_guests_nationality"); // Changed from e.Nationality to e.NationalityType
                entity.HasIndex(e => e.NationalityId).HasDatabaseName("IX_guests_nationality_id"); // Add index on foreign key
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

                // If you want indexes on any columns, you can add here, for example:
                // entity.HasIndex(e => e.OwnerName).HasDatabaseName("IX_operator_list_owner_name");
            });

            modelBuilder.Entity<Nationality>().ToTable("nationalities");
        }
    }
}