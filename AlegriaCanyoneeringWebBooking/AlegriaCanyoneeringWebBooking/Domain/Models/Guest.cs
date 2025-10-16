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
        public string? Fullname { get; set; }   // <-- Remove [Required] for batch

        [RegularExpression(@"^\d+$", ErrorMessage = "Age must contain only numbers.")]
        [StringLength(3, ErrorMessage = "Age cannot exceed 3 digits.")]
        [Display(Name = "Age")]
        public string? Age { get; set; }



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
        public int BookingStatus { get; set; } = 0; // Default to 'anticipated' (0)


        public enum BookingStatusEnum
        {
            anticipated = 0,  // 'anticipated' instead of 'Anticipated'
            canceled = 1,     // 'canceled' instead of 'Canceled'
            reserved = 2,     // 'reserved' instead of 'Reserved'
            confirmed = 3     // 'confirmed' instead of 'Confirmed'
        }


        [Column("Area")]
        public string? Area { get; set; }

        [Column("ContactNum")]
        public string? ContactNumber { get; set; }

        [Column("Id")]
        public int? id { get; set; }
        public OperatorList? OperatorList { get; set; }

        // Foreign key property

        [Column("operatorid")]
        public int? OperatorId { get; set; }

        public Operator? Operators { get; set; }
        [NotMapped] public string? QRBase64 { get; set; }
        [NotMapped] public string? QRText { get; set; }

    }
}
