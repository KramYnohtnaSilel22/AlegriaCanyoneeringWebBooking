using AlegriaCanyoneeringWebBooking.Models;
using System.ComponentModel.DataAnnotations;

namespace AlegriaCanyoneeringWebBooking.ViewModel
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


        // Add this 👇 for image upload
        [DataType(DataType.Upload)]
        public IFormFile Photo { get; set; }


    }
}
