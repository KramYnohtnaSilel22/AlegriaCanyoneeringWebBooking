using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    public class Guest
    {
        [Key]
        public int GuestId { get; set; }

        [StringLength(1000, ErrorMessage = "Full name cannot exceed 1000 characters")]
        [Display(Name = "Full Name")]
        public string? Fullname { get; set; }   // <-- Remove [Required] for batch

        [StringLength(1000, ErrorMessage = "Age cannot exceed 1000 characters")]
        [Display(Name = "Age")]
        public string? Age { get; set; }        // <-- Remove [Required] for batch

        [Display(Name = "Number of Guests")]
        [Column("number_of_guests")]
        public int NumberOfGuests { get; set; }

        [Display(Name = "Batch")]
        [StringLength(100)]
        public string? Batch { get; set; }

        [StringLength(10000, ErrorMessage = "Nationality cannot exceed 10000 characters")]
        [Display(Name = "Nationality")]
        public string? NationalityType { get; set; }

        // Foreign Key to Nationality
        public int? NationalityId { get; set; }

        // Navigation property for related Nationality
        public Nationality Nationality { get; set; } // Ensure this navigation property is added


        [StringLength(1000, ErrorMessage = "Gender cannot exceed 1000 characters")]
        [Display(Name = "Gender")]
        public string? Gender { get; set; }     // <-- Remove [Required] for batch

        [StringLength(1000, ErrorMessage = "Date cannot exceed 1000 characters")]
        [Display(Name = "Date")]
        public string? Date { get; set; }       // <-- Remove [Required] for batch

        [StringLength(100, ErrorMessage = "Arrival date cannot exceed 100 characters")]
        [Display(Name = "Arrival Date")]
        [Column("arrivaldate")]
        public string? ArrivalDate { get; set; } // <-- Remove [Required] for batch

        public string? Month { get; set; }

        [StringLength(100, ErrorMessage = "Short date cannot exceed 100 characters")]
        [Display(Name = "Short Date")]
        [Column("dateshort")]
        public string? DateShort { get; set; }

        [Column("rfid")]
        public string? RFID { get; set; }

        [StringLength(50)]
        [Column("bookingstatus")]
        public string BookingStatus { get; set; } = "anticipated";

        [Column("qrcode")]
        public string? QrCode { get; set; }

        [Column("Area")]
        public string? Area { get; set; }

        [Column("ContactNum")]
        public string? ContactNumber { get; set; }

        [Column("operatorid")]
        public int? OperatorId { get; set; }
        public OperatorList? OperatorList { get; set; }

        [Column("DriverId")]
        public int? DriverId { get; set; }
        public Driver? Driver { get; set; }

        [Column("GuideId")]
        public int? GuideId { get; set; }
        [ForeignKey(nameof(GuideId))]
        public Guide? Guide { get; set; }

        [NotMapped] public string? QRBase64 { get; set; }
        [NotMapped] public string? QRText { get; set; }
    }
}
