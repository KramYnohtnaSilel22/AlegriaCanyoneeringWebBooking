using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking
{
    [Table("driver_details")]
    public class Driver
    {

        [Key]

        public int DriverId { get; set; }  // bigint → long in C#

        [Column("dposition")]
        public int DPosition { get; set; }  // int(99) → int in C#

        [Required]
        [Column("refid")]
        [StringLength(99)]
        public string RefId { get; set; }

        [Required]
        [Column("fname")]
        public string FName { get; set; }  // mediumtext → string

        [Required]
        [Column("mname")]
        public string MName { get; set; }

        [Required]
        [Column("lname")]
        public string LName { get; set; }

        [Required]
        [Column("pnumber")]
        public string PNumber { get; set; }

        [Required]
        [Column("cnumber")]
        public string CNumber { get; set; }

        [Required]
        [Column("address")]
        public string Address { get; set; }

        [Required]
        [Column("image")]
        public string Image { get; set; }



        // ✅ Add this navigation property
        public ICollection<Guest> Guests { get; set; } = new List<Guest>();
    }
}
