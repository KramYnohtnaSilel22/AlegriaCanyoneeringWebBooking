using AlegriaCanyoneeringWebBooking.Models;

namespace AlegriaCanyoneeringWebBooking.ViewModel
{
    public class GuestDetailsViewModel
    {

        public Guest Guest { get; set; }
        public List<Guest> GuestsInBatch { get; set; }
        // Add this:
        public string CurrentBatch { get; set; }
      
        public string BookingStatusDisplay
        {
            get
            {
                // Ensure that the BookingStatus is within the valid range of the enum
                if (Enum.IsDefined(typeof(BookingStatusEnum), Guest.BookingStatus))
                {
                    // Convert the integer status to the corresponding enum and get its name
                    return Enum.GetName(typeof(BookingStatusEnum), Guest.BookingStatus);
                }
                return "Unknown";  // Fallback if the status is not valid
            }
        }
    }
}
