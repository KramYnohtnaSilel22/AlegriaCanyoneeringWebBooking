using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("tourguide_details")]
    public class Guide
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("rfid")]
        [StringLength(200)]
        public string Rfid { get; set; }

        [Required]
        [Column("tposition")]
        public int TPosition { get; set; }

        [Required]
        [Column("fname")]
        [StringLength(100)]
        public string FName { get; set; }

        [Column("mname")]
        [StringLength(100)]
        public string? MName { get; set; }

        [Required]
        [Column("lname")]
        [StringLength(100)]
        public string LName { get; set; }

        [Required]
        [Column("cnumber")]
        [StringLength(20)]
        public string CNumber { get; set; }

        [Required]
        [Column("address")]
        [StringLength(255)]
        public string Address { get; set; }

        [Column("nickname")]
        [StringLength(100)]
        public string? Nickname { get; set; }

        [Column("image")]
        [StringLength(255)]
        public string? Image { get; set; }

        // ✅ Add this navigation property
        public ICollection<Guest> Guests { get; set; } = new List<Guest>();

    }
}
