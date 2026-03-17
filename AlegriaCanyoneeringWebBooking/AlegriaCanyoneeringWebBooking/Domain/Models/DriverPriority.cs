using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("driver_priority")]
    public class DriverPriority
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("driver_idprior")]
        public int DriverIdPrior { get; set; }   // int cast of Driver.RefId

        [Column("date")]
        public string? Date { get; set; }         // Unix timestamp string

        [Column("passenger")]
        public int Passenger { get; set; }
    }
}