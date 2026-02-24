using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("tourguide_attendance")]
    public class TourGuideAttendance
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("TG_id")]
        public string TGId { get; set; }

        [Column("date")]
        public string? Date { get; set; }

        [Column("rfid")]
        public string? Rfid { get; set; }
    }
}