using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("reserve")]  // Table name in database
    public class Reserve
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Fullname { get; set; }

        public int Age { get; set; }

        [StringLength(10)]
        public string Gender { get; set; }

        [StringLength(50)]
        public string Nationality { get; set; }

        [StringLength(50)]
        public string NatStat { get; set; } // National Status

        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [DataType(DataType.Date)]
        public DateTime ArrivalDate { get; set; }

        [StringLength(20)]
        public string Month { get; set; }

        [StringLength(20)]
        public string DateShort { get; set; }

        [StringLength(50)]
        public string RFID { get; set; }

        [Required]
        [StringLength(20)]
        public string BookingStatus { get; set; }
 

    }
}
