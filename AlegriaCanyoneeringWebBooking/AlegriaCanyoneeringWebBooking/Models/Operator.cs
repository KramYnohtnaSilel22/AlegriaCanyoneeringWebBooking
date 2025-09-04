using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    public class Operator
    {
        [Key]
        [Column("operatorid")]
        public int OperatorId { get; set; }

        [Required, MaxLength(1000)]
        [Column("owner_name")]
        public string OwnerName { get; set; }

        [Required, MaxLength(1000)]
        [Column("gender")]
        public string Gender { get; set; }

        [Required, MaxLength(1000)]
        [Column("business_name")]
        public string BusinessName { get; set; }

        [Required, MaxLength(1000)]
        [Column("buss_permit")]
        public string BussPermit { get; set; }

        [Required, MaxLength(1000)]
        [Column("location")]
        public string Location { get; set; }

        [Required]
        [Column("status")]
        public int Status { get; set; }
        // Navigation property to Guests
        [NotMapped]
        [Column("is_active")]
        public bool IsActive { get; set; }

        // Navigation property for related Guests
   
        public ICollection<Guest> Guests { get; set; } = new List<Guest>();
    }
}
