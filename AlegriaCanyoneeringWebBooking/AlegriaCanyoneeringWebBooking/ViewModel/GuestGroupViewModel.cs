using AlegriaCanyoneeringWebBooking.Models;

namespace AlegriaCanyoneeringWebBooking.ViewModel
{
    public class GuestGroupViewModel
    {
        public int? OperatorId { get; set; }
        public OperatorList OperatorList { get; set; }
        public int ActiveGuestCount { get; set; }
        public string ArrivalDate { get; set; }
        public string BookingStatus { get; set; }

        public string Batch { get; set; }
    }
}
