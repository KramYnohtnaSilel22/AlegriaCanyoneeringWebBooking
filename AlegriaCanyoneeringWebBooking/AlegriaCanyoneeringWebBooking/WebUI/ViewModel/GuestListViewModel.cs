
using System.ComponentModel.DataAnnotations;

namespace AlegriaCanyoneeringWebBooking
{
    public class GuestListViewModel
    {
        public Guest NewGuest { get; set; } = new Guest();
        public List<Guest> ReservedGuests { get; set; } = new List<Guest>();
        public string? BatchQrBase64 { get; set; }  // single QR for the whole batch
        public Dictionary<string, List<Guest>> BatchGuests { get; set; }
        public string BatchGuestsJson { get; set; }

        // Read-only values
        public int? ReadOnlyOperatorId { get; set; }
        public DateTime? ReadOnlyArrivalDate { get; set; }
        public DateTime? ReadOnlyBookingDate { get; set; }
        public string ReadOnlyTourArea { get; set; }
        public int? ReadOnlyNationalityId { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        // Add ArrivalDateUnixTimestamp to the ViewModel
        public string ArrivalDate { get; set; } // Human-readable arrival date from user input
        public long ArrivalDateUnixTimestamp { get; set; }  // Store Unix timestamp (not in the database)
        // ✅ Add properties for HTML5 date inputs
        public string Html5BookingDate => DateTime.TryParse(NewGuest.Date, out var bookingDate)
            ? bookingDate.ToString("yyyy-MM-dd")
            : "";

        public string Html5ArrivalDate => DateTime.TryParse(NewGuest.ArrivalDate, out var arrivalDate)
            ? arrivalDate.ToString("yyyy-MM-dd")
            : "";
        // Add this 👇 for image upload
        [DataType(DataType.Upload)]
        public IFormFile Photo { get; set; }


    }
}
