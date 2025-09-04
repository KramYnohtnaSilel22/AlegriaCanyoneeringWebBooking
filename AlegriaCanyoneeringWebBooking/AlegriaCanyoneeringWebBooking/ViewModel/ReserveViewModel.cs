using AlegriaCanyoneeringWebBooking.Models;

namespace AlegriaCanyoneeringWebBooking.ViewModel
{
    public class ReserveViewModel
    {
   
            public Guest Guest { get; set; }              // For the left-side form
            public IEnumerable<Guest> Guests { get; set; } // For the right-side table
        }

    }

