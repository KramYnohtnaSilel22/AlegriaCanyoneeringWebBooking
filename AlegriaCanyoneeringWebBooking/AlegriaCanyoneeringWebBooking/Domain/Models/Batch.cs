using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking
{
    [Table("tblbatch")]
    public class Batch
    {
        [Key]
        [Column("batchid")]
        public int BatchId { get; set; }

        [Required]
        [Column("operatorname")]
        [StringLength(200)]
        public int OperatorId { get; set; }

        [ForeignKey(nameof(OperatorId))]
        public OperatorList? OperatorList { get; set; }

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

        // Since you used varchar(100) for date, I recommend storing it as DateTime
        public DateTime ArrivalDate { get; set; }

        // 👉 You can change this to DateTime if you want:
        // public DateTime ArrivalDate { get; set; }
    }
}
