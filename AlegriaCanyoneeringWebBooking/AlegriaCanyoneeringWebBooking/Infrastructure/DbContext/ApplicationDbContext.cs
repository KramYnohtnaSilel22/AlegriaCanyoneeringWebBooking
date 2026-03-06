
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
            // Create Role table if not exists
            Database.ExecuteSqlRaw(@"
    CREATE TABLE IF NOT EXISTS `role` (
        `RoleId` INT NOT NULL,
        `Name` VARCHAR(255) NOT NULL,
        PRIMARY KEY (`RoleId`)
    );
");

            // Seed default roles if not exist
            Database.ExecuteSqlRaw(@"
    INSERT INTO `role` (RoleId, Name)
    SELECT * FROM (SELECT 1 AS RoleId, 'Admin' AS Name) AS tmp
    WHERE NOT EXISTS (SELECT 1 FROM `role` WHERE RoleId = 1);
");

            Database.ExecuteSqlRaw(@"
    INSERT INTO `role` (RoleId, Name)
    SELECT * FROM (SELECT 2 AS RoleId, 'Operator' AS Name) AS tmp
    WHERE NOT EXISTS (SELECT 1 FROM `role` WHERE RoleId = 2);
");

            Database.ExecuteSqlRaw(@"
    INSERT INTO `role` (RoleId, Name)
    SELECT * FROM (SELECT 3 AS RoleId, 'Super Admin' AS Name) AS tmp
    WHERE NOT EXISTS (SELECT 1 FROM `role` WHERE RoleId = 3);
");

            Database.ExecuteSqlRaw(@"
    INSERT INTO `role` (RoleId, Name)
    SELECT * FROM (SELECT 4 AS RoleId, 'Staff' AS Name) AS tmp
    WHERE NOT EXISTS (SELECT 1 FROM `role` WHERE RoleId = 4);
");

        }
        public DbSet<Batch> Batches { get; set; }
        public DbSet<Guest> Guests { get; set; }
        public DbSet<OperatorList> OperatorLists { get; set; }
        public DbSet<GuestImage> GuestImage { get; set; }
        public DbSet<Nationality> Nationalities { get; set; }

        // //public DbSet<Driver> Drivers { get; set; }
     
        public DbSet<Role> Roles { get; set; }
        public DbSet<Operator> Operators { get; set; }
        public DbSet<GuestBriefing> GuestBriefings { get; set; }

        public DbSet<Driver> Drivers { get; set; }

        public DbSet<Guide> Guides { get; set; }

        public DbSet<DriverAttendance> DriverAttendances { get; set; }
        public DbSet<DriverDtr> DriverDtrs { get; set; }

        public DbSet<DriverIdPrior> DriverIdPriors { get; set; }
 
        public DbSet<TourGuideAttendance> TourGuideAttendances { get; set; }
        public DbSet<TourGuideDtr> TourGuideDtrs { get; set; }

        public DbSet<BatchAssignment> BatchAssignments { get; set; }


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
         
            modelBuilder.Entity<Role>().ToTable("role");
            modelBuilder.Entity<Operator>().ToTable("tbl_operator_mobile");
            modelBuilder.Entity<GuestBriefing>().ToTable("tbl_guestbreifing");
            modelBuilder.Entity<Driver>().ToTable("driver_details");
            modelBuilder.Entity<Guide>().ToTable("tourguide_details");
            modelBuilder.Entity<DriverAttendance>().ToTable("driver_attendance");
            modelBuilder.Entity<DriverDtr>().ToTable("driver_dtr");
            modelBuilder.Entity<DriverIdPrior>().ToTable("driver_priority");
            modelBuilder.Entity<TourGuideAttendance>().ToTable("tourguide_attendance");
            modelBuilder.Entity<TourGuideDtr>().ToTable("tourguide_dtr");
            modelBuilder.Entity<BatchAssignment>().ToTable("tbl_batch_assignments");

        }
    }
}
