using AlegriaCanyoneeringWebBooking.Models;

namespace AlegriaCanyoneeringWebBooking.ViewModel
{
    public class GuestDetailsViewModel
    {

        public Guest Guest { get; set; }
        public List<Guest> GuestsInBatch { get; set; }
        // Add this:
        public string CurrentBatch { get; set; }
    }
}
