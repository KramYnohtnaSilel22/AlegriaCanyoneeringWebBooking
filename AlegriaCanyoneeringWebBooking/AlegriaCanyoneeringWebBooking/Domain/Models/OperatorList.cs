using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking
{
    [Table("operator_list")]
    public class OperatorList
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

        // NotMapped property
        [NotMapped]
        public bool IsActive => Status == 1;

        // Navigation property for related Guests

        public ICollection<Guest> Guests { get; set; } = new List<Guest>();
    }
}
