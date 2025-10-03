using AlegriaCanyoneeringWebBooking.Models;
using System.ComponentModel.DataAnnotations;

namespace AlegriaCanyoneeringWebBooking.ViewModel
{
    public class GuestListViewModel
    {
        public Guest NewGuest { get; set; } = new Guest();
        public List<GuestGroupViewModel> GuestGroups { get; set; } = new List<GuestGroupViewModel>();
        public string? BatchQrBase64 { get; set; }
        public Dictionary<string, List<Guest>> BatchGuests { get; set; }
        public string BatchGuestsJson { get; set; }
        public int? ReadOnlyOperatorId { get; set; }
        public DateTime? ReadOnlyArrivalDate { get; set; }
        public DateTime? ReadOnlyBookingDate { get; set; }
        public string ReadOnlyTourArea { get; set; }
        public int? ReadOnlyNationalityId { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public List<Guest> ReservedGuests { get; set; } = new List<Guest>();
        // ✅ Add this property
        public List<Guest> GuestsInBatch { get; set; } = new List<Guest>();

        public string Html5BookingDate => DateTime.TryParse(NewGuest.Date, out var bookingDate)
            ? bookingDate.ToString("yyyy-MM-dd")
            : "";
        public string Html5ArrivalDate => DateTime.TryParse(NewGuest.ArrivalDate, out var arrivalDate)
            ? arrivalDate.ToString("yyyy-MM-dd")
            : "";
        [DataType(DataType.Upload)]
        public IFormFile Photo { get; set; }
    }
}