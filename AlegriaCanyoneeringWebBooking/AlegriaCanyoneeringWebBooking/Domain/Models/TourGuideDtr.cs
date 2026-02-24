using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("tourguide_dtr")]
    public class TourGuideDtr
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("rfid")]
        public long Rfid { get; set; }

        [Column("date")]
        public long Date { get; set; }

        [Column("no_of_guest")]
        public string? NoOfGuest { get; set; }

        [Column("comdate")]
        public string? ComDate { get; set; }
    }
}