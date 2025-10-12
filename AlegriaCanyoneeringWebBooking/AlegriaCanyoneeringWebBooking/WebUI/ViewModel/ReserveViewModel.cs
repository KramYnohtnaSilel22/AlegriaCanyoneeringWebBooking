

using AlegriaCanyoneeringWebBooking.Models;

namespace AlegriaCanyoneeringWebBooking
{
    public class ReserveViewModel
    {

        public Guest Guest { get; set; }              // For the left-side form
        public IEnumerable<Guest> Guests { get; set; } // For the right-side table
    }

}

