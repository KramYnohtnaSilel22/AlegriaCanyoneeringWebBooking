using AlegriaCanyoneeringWebBooking.Models;

namespace AlegriaCanyoneeringWebBooking.ViewModel
{
    public class BatchBookingViewModel
    {
        public Guest MainGuest { get; set; }
        public List<Guest> OtherGuests { get; set; }
    }

}
