using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("driver_attendance")]
    public class DriverAttendance
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("driver_id")]
        public string DriverId { get; set; }

        [Required]
        [Column("date")]
        public string Date { get; set; }

        [Column("passenger")]
        public int Passenger { get; set; }
    }
}