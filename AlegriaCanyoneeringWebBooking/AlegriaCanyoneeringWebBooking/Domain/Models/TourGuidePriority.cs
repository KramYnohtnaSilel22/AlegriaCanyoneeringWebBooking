using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Domain.Models
{
    [Table("tourguide_priority")]
    public class TourGuidePriority
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("guide_idprior")]
        public int GuideIdPrior { get; set; }

        [Column("date")]
        public string? Date { get; set; }

        [Column("NoOfGuest")]
        public int NoOfGuest { get; set; }
    }
}
