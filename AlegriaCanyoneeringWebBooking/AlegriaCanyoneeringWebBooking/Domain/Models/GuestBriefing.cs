using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Domain.Models
{
    [Table("tbl_guestbreifing")]
    public class GuestBriefing
    {
        [Key]
        public int BGuestID { get; set; }

        public string BWristBondCode { get; set; } = string.Empty;

        public string? BGuestName { get; set; }

        public string? BDateArrival { get; set; }

        public string? BDateDeparture { get; set; }

        public string? BDateCode { get; set; } // stored as Unix timestamp

        public byte[]? BGuestImage { get; set; } // optional image
    }

}
