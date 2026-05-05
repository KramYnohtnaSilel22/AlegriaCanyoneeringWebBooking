using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    public class Guest
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }


        [StringLength(1000, ErrorMessage = "Full name cannot exceed 1000 characters")]
        [Display(Name = "Full Name")]
        [RegularExpression(@"^[A-Za-zÑñ\s]+$", ErrorMessage = "Full name can only contain letters, Ñ/ñ, and spaces")]
        public string? Fullname { get; set; }


        [RegularExpression(@"^\d+$", ErrorMessage = "Age must contain only numbers.")]
        [StringLength(3, ErrorMessage = "Age cannot exceed 3 digits.")]
        [Display(Name = "Age")]
        public string? Age { get; set; }


        // ✅
        [Column("batchcode")]
        [Display(Name = "Batch")]
        [StringLength(100)]
        public string? Batch { get; set; }

        [StringLength(10000, ErrorMessage = "Nationality cannot exceed 10000 characters")]
        [Display(Name = "Nationality")]
        public string? NationalityType { get; set; }

        // Foreign Key to Nationality
        public int? NationalityId { get; set; }

        // Navigation property for related Nationality
        public Nationality? NationalityEntity { get; set; } // ✅ Isa ra ni


        [StringLength(1000, ErrorMessage = "Gender cannot exceed 1000 characters")]
        [Display(Name = "Gender")]
        public string? Gender { get; set; }     // <-- Remove [Required] for batch

        [StringLength(1000, ErrorMessage = "Date cannot exceed 1000 characters")]
        [Display(Name = "Date")]
        public string? Date { get; set; }       // <-- Remove [Required] for batch

        [StringLength(100, ErrorMessage = "Arrival date cannot exceed 100 characters")]
        [Display(Name = "Arrival Date")]
        [Column("arrivaldate")]
        public string? ArrivalDate { get; set; }  // This stores the Unix timestamp as a string
        public string? Month { get; set; }

        [StringLength(100, ErrorMessage = "Short date cannot exceed 100 characters")]
        [Display(Name = "Short Date")]
        [Column("dateshort")]
        public string? DateShort { get; set; }
        [Column("rfid")]
        public int? RFID { get; set; }  // system-assigned guest code

        [Column("rfIDCode")]
        public string? RFIDCode { get; set; }  // real tag code from RFID card


        [Column("year")]
        public string? Year { get; set; }

        [Column("status")]
        public int BookingStatus { get; set; } = 3; // Default to 'anticipated' (3)


        public enum BookingStatusEnum
        {

            confirmed = 1,
            reserved = 2,
            anticipated = 3,
            canceled = 4

        }

        // ← ADD THIS
        public static class Status
        {
            public const int Confirmed = 1;
            public const int Reserved = 2;
            public const int Anticipated = 3;
            public const int Canceled = 4;
        }




        [Column("Area")]
        public string? Area { get; set; }

        [Column("ContactNum")]
        [StringLength(11, MinimumLength = 11, ErrorMessage = "Contact number must be exactly 11 digits")]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "Contact number must contain only numbers")]
        [Display(Name = "Mobile Number")]
        public string? ContactNumber { get; set; }

        // Foreign key property

        [Column("operatorid")]
        public int? OperatorId { get; set; }
        [NotMapped]
        public OperatorList? OperatorList { get; set; }
        [NotMapped] public string? QRBase64 { get; set; }
        [NotMapped] public string? QRText { get; set; }
      
    }
}
