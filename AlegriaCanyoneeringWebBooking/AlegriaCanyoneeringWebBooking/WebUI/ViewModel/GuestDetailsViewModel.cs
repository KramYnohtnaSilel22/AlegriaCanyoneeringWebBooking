
using AlegriaCanyoneeringWebBooking.Models;
using static AlegriaCanyoneeringWebBooking.Models.Guest;

namespace AlegriaCanyoneeringWebBooking
{
    public class GuestDetailsViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public DateTime? ArrivalDate { get; set; }
        public DateTime? DepartureDate { get; set; }
        public string? Operators { get; set; }
        public string? Age { get; set; }
        public string? Nationality { get; set; }
        public string WristbandCode { get; set; } = string.Empty;
        public string? QRText { get; set; }

        public string? GuestImageBase64 { get; set; }
        public string? QRBase64 { get; set; } // optional if QR image available
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
