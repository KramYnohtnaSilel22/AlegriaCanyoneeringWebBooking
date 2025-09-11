using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    public class Guest
    {
        [Key]
        public int GuestId { get; set; }

        [Required(ErrorMessage = "Full name is required")]
        [StringLength(1000, ErrorMessage = "Full name cannot exceed 1000 characters")]
        [Display(Name = "Full Name")]
        public string Fullname { get; set; }

        [Required(ErrorMessage = "Age is required")]
        [StringLength(1000, ErrorMessage = "Age cannot exceed 1000 characters")]
        [Display(Name = "Age")]
        public string Age { get; set; }

        [Display(Name = "Number of Guests")]
        [Column("number_of_guests")]
        public int NumberOfGuests { get; set; }

        [Display(Name = "Batch")]
        [StringLength(100)]
        public string? Batch { get; set; }

        [StringLength(10000, ErrorMessage = "Nationality cannot exceed 10000 characters")]
        [Display(Name = "Nationality")]
        public string? NationalityType { get; set; } // This maps to 'nationality' column

        [Display(Name = "National Status")]
        [Column("natstat")] // This is the key fix - map to the correct database column
        public int? NationalityId { get; set; } // Make this nullable to handle NULL values

        [ForeignKey(nameof(NationalityId))]
        public Nationality? Nationality { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        [StringLength(1000, ErrorMessage = "Gender cannot exceed 1000 characters")]
        [Display(Name = "Gender")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "Date is required")]
        [StringLength(1000, ErrorMessage = "Date cannot exceed 1000 characters")]
        [Display(Name = "Date")]
        public string Date { get; set; }

        [Required(ErrorMessage = "Arrival date is required")]
        [StringLength(100, ErrorMessage = "Arrival date cannot exceed 100 characters")]
        [Display(Name = "Arrival Date")]
        [Column("arrivaldate")]
        public string ArrivalDate { get; set; }

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
        public Operator? Operator { get; set; }

        [Column("DriverId")] // Make sure this matches your database column name
        public int? DriverId { get; set; }
        public Driver? Driver { get; set; }

        [Column("GuideId")] // Make sure this also matches
        public int? GuideId { get; set; }

        [ForeignKey(nameof(GuideId))]
        public Guide? Guide { get; set; }

        [NotMapped]
        public string QRBase64 { get; set; }
    }
}