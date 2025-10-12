using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("reserve")]  // Table name in database
    public class Reserve
    {
        public int ReserveId { get; set; }
        public string BatchCode { get; set; }
        public int? OperatorId { get; set; }
        public int TotalGuests { get; set; }

        public string Status { get; set; }
        public DateTime ArrivalDate { get; set; }
    }

}
