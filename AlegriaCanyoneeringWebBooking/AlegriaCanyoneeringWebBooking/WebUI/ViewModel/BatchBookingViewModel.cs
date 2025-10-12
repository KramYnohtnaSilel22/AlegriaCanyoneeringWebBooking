
using AlegriaCanyoneeringWebBooking.Models;

namespace AlegriaCanyoneeringWebBooking
{
    public class BatchBookingViewModel
    {
        public Guest MainGuest { get; set; }
        public List<Guest> OtherGuests { get; set; }
    }

}
