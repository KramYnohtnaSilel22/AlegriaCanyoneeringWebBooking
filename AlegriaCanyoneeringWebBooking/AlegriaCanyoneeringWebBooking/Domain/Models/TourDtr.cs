using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("tour_dtr")]
    public class TourDtr
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("rfid")]
        public int Rfid { get; set; }

        [Column("date")]
        public string? Date { get; set; }

        [Column("passenger")]
        public string? Passenger { get; set; }

        [Column("comdatedr")]
        public string? ComDateDr { get; set; }
    }
}