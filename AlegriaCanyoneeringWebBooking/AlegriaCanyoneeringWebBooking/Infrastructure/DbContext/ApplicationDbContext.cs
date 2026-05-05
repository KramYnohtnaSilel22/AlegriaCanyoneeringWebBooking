using AlegriaCanyoneeringWebBooking.Domain.Models;
using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AlegriaCanyoneeringWebBooking
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
            Database.ExecuteSqlRaw(@"
    CREATE TABLE IF NOT EXISTS `role` (
        `RoleId` INT NOT NULL,
        `Name` VARCHAR(255) NOT NULL,
        PRIMARY KEY (`RoleId`)
    );");

            Database.ExecuteSqlRaw(@"
    INSERT INTO `role` (RoleId, Name)
    SELECT * FROM (SELECT 1 AS RoleId, 'Admin' AS Name) AS tmp
    WHERE NOT EXISTS (SELECT 1 FROM `role` WHERE RoleId = 1);");

            Database.ExecuteSqlRaw(@"
    INSERT INTO `role` (RoleId, Name)
    SELECT * FROM (SELECT 2 AS RoleId, 'Operator' AS Name) AS tmp
    WHERE NOT EXISTS (SELECT 1 FROM `role` WHERE RoleId = 2);");

            Database.ExecuteSqlRaw(@"
    INSERT INTO `role` (RoleId, Name)
    SELECT * FROM (SELECT 3 AS RoleId, 'Super Admin' AS Name) AS tmp
    WHERE NOT EXISTS (SELECT 1 FROM `role` WHERE RoleId = 3);");

            Database.ExecuteSqlRaw(@"
    INSERT INTO `role` (RoleId, Name)
    SELECT * FROM (SELECT 4 AS RoleId, 'Staff' AS Name) AS tmp
    WHERE NOT EXISTS (SELECT 1 FROM `role` WHERE RoleId = 4);");
        }

        public DbSet<Batch> Batches { get; set; }
        public DbSet<Guest> Guests { get; set; }
        public DbSet<OperatorList> OperatorLists { get; set; }
        public DbSet<GuestImage> GuestImage { get; set; }
        public DbSet<Nationality> Nationalities { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Operator> Operators { get; set; }
        public DbSet<GuestBriefing> GuestBriefings { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<Guide> Guides { get; set; }
        public DbSet<DriverAttendance> DriverAttendances { get; set; }
        public DbSet<DriverDtr> DriverDtrs { get; set; }
        public DbSet<DriverPriority> DriverPriorities { get; set; }
        public DbSet<TourGuideAttendance> TourGuideAttendances { get; set; }
        public DbSet<TourGuideDtr> TourGuideDtrs { get; set; }
        public DbSet<BatchAssignment> BatchAssignments { get; set; }
        public DbSet<TourGuidePriority> TourGuidePriorities { get; set; }
        public DbSet<OutsideGuide> OutsideGuides { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var operatorIdConverter = new ValueConverter<int?, string>(
                v => v.HasValue ? v.Value.ToString() : null,
                v => string.IsNullOrEmpty(v) ? (int?)null : int.Parse(v)
            );

            // ── Batch ────────────────────────────────────────────────
            modelBuilder.Entity<Batch>(entity =>
            {
                entity.ToTable("tblbatch");
                entity.Property(e => e.OperatorId)
                      .HasConversion(operatorIdConverter)
                      .HasColumnName("operatorname");
            });

            // ── Guest ────────────────────────────────────────────────
            modelBuilder.Entity<Guest>(entity =>
            {
                entity.ToTable("guest");

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Fullname).HasColumnName("fullname").HasMaxLength(1000);
                entity.Property(e => e.Age).HasColumnName("age").HasMaxLength(1000);
                entity.Property(e => e.Batch).HasColumnName("batchcode").HasMaxLength(100);
                entity.Property(e => e.NationalityType).HasColumnName("nationality").HasMaxLength(10000);
                entity.Property(e => e.NationalityId).HasColumnName("nat_stat");
                entity.Property(e => e.Gender).HasColumnName("gender").HasMaxLength(1000);
                entity.Property(e => e.Date).HasColumnName("date").HasMaxLength(1000);
                entity.Property(e => e.ArrivalDate).HasColumnName("arrivaldate").HasMaxLength(100);
                entity.Property(e => e.Month).HasColumnName("month");
                entity.Property(e => e.DateShort).HasColumnName("dateshort").HasMaxLength(100);
                entity.Property(e => e.Year).HasColumnName("year");
                entity.Property(e => e.BookingStatus).HasColumnName("status").HasDefaultValue(0);
                entity.Property(e => e.RFID).HasColumnName("rfid");
                entity.Property(e => e.RFIDCode).HasColumnName("rfidcode").HasMaxLength(1000);
                entity.Property(e => e.Area).HasColumnName("Area").HasMaxLength(500);
                entity.Property(e => e.ContactNumber).HasColumnName("ContactNum").HasMaxLength(500);

                // ✅ Converter only — NO HasOne/WithMany since OperatorList is [NotMapped]
                entity.Property(e => e.OperatorId)
                      .HasConversion(operatorIdConverter)
                      .HasColumnName("operatorid");

                // Nationality FK is fine — both sides are int
                entity.HasOne(g => g.NationalityEntity)
                      .WithMany()
                      .HasForeignKey(g => g.NationalityId)
                      .HasConstraintName("FK_Guest_Nationality")
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired(false);

                entity.HasIndex(e => e.Fullname).HasDatabaseName("IX_guests_fullname");
                entity.HasIndex(e => e.RFID).HasDatabaseName("IX_guests_rfid");
                entity.HasIndex(e => e.NationalityType).HasDatabaseName("IX_guests_nationality");
                entity.HasIndex(e => e.NationalityId).HasDatabaseName("IX_guests_nationality_id");
                entity.HasIndex(e => e.BookingStatus).HasDatabaseName("IX_guests_booking_status");
            });

            // ── OperatorList ─────────────────────────────────────────
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

            // ── Other tables ─────────────────────────────────────────
            modelBuilder.Entity<Nationality>().ToTable("nationalities");
            modelBuilder.Entity<Role>().ToTable("role");
            modelBuilder.Entity<Operator>().ToTable("tbl_operator_mobile");
            modelBuilder.Entity<GuestBriefing>().ToTable("tbl_guestbreifing");
            modelBuilder.Entity<Driver>().ToTable("driver_details");
            modelBuilder.Entity<Guide>().ToTable("tourguide_details");
            modelBuilder.Entity<DriverAttendance>().ToTable("driver_attendance");
            modelBuilder.Entity<DriverDtr>().ToTable("driver_dtr");
            modelBuilder.Entity<DriverPriority>().ToTable("driver_priority");
            modelBuilder.Entity<TourGuideAttendance>().ToTable("tourguide_attendance");
            modelBuilder.Entity<TourGuideDtr>().ToTable("tourguide_dtr");
            modelBuilder.Entity<BatchAssignment>().ToTable("tbl_batch_assignments");
            modelBuilder.Entity<TourGuidePriority>().ToTable("tourguide_priority");
            modelBuilder.Entity<OutsideGuide>().ToTable("outside_tourguide_details");
        }
    }
}