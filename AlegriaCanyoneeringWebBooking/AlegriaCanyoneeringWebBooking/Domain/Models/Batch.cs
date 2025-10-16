using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("tblbatch")]
    public class Batch
    {
        [Key]
        [Column("batchid")]
        public int BatchId { get; set; }

        [Required]
        [Column("operatorname")]
        public int OperatorId { get; set; }

        [ForeignKey(nameof(OperatorId))]
        public Operator? Operators { get; set; }

        [Column("no_of_localguest")]
        public int NoOfLocalGuest { get; set; }

        [Column("no_of_foreignguest")]
        public int NoOfForeignGuest { get; set; }

        [Column("no_of_tguide")]
        public int NoOfTGuide { get; set; }

        [Column("no_of_mdriver")]
        public int NoOfMDriver { get; set; }

        [Column("total_no_of_guest")]
        public int TotalNoOfGuest { get; set; }

        [Column("arrivaldate")]
        public string ArrivalDate { get; set; } = string.Empty;



    }
}
