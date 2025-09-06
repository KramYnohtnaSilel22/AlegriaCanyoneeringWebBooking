using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    public class Guest
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Full name is required")]
        [StringLength(1000, ErrorMessage = "Full name cannot exceed 1000 characters")]
        [Display(Name = "Full Name")]
        public string Fullname { get; set; }

        [Required(ErrorMessage = "Age is required")]
        [StringLength(1000, ErrorMessage = "Age cannot exceed 1000 characters")]
        [Display(Name = "Age")]
        public string Age { get; set; }

        [Display(Name = "Number of Guests")]
        public int NumberOfGuests { get; set; }

        [Display(Name = "Batch")]
        [StringLength(100)]
        public string? Batch { get; set; }
        [Required(ErrorMessage = "Nationality is required")]
        [StringLength(10000, ErrorMessage = "Nationality cannot exceed 10000 characters")]
        [Display(Name = "Nationality")]
        public string NationalityType { get; set; }

        [Required(ErrorMessage = "National status is required")]
        [Display(Name = "National Status")]
        public int NationalityId { get; set; }

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
        public string ArrivalDate { get; set; }

        [Required(ErrorMessage = "Month is required")]
        [StringLength(500, ErrorMessage = "Month cannot exceed 500 characters")]
        [Display(Name = "Month")]
        public string Month { get; set; }

    
        [StringLength(100, ErrorMessage = "Short date cannot exceed 100 characters")]
        [Display(Name = "Short Date")]
        public string DateShort { get; set; }

        [Required(ErrorMessage = "RFID is required")]
        [Display(Name = "RFID")]
        public int RFID { get; set; }
        public string BookingStatus { get; set; } = "anticipated";

        public string? QrCode { get; set; }
        [ForeignKey("Operator")]
        [Column("operatorid")]
        public int? OperatorId { get; set; }

        public Operator? Operator { get; set; }  // navigation



    }
}
