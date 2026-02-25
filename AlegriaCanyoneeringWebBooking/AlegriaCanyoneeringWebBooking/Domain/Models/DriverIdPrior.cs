using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("driver_priority")]
    public class DriverIdPrior
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("driver_idprior")]
        public int DriverIdPriorValue { get; set; }

        [Column("date")]
        public string? Date { get; set; }

        [Column("passenger")]
        public int Passenger { get; set; }
    }
}