using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("driver_details")]
    public class Driver
    {

        [Key]
        [Column("id")]
        public int DriverId { get; set; }

        [Column("dposition")]
        public int DPosition { get; set; }

        [Required]
        [Column("refid")]
        [StringLength(99)]
        public string RefId { get; set; }

        [Required]
        [Column("fname")]
        public string FName { get; set; }

        // ✅ NOT Required — optional middle name
        [Column("mname")]
        public string? MName { get; set; }

        [Required]
        [Column("lname")]
        public string LName { get; set; }

        [Column("pnumber")]
        public string? PNumber { get; set; }

        [Column("cnumber")]
        public string? CNumber { get; set; }

        [Column("ctcdate")]
        public string? CtcDate { get; set; } // ✅ nullable — not always required

        [Column("address")]
        public string? Address { get; set; } // ✅ nullable — not always required


        // ✅ Image stored as file path string, not required on form
        [Column("image")]
        public string? Image { get; set; }


        // ✅ Add this navigation property
        public ICollection<Guest> Guests { get; set; } = new List<Guest>();
    }
}
