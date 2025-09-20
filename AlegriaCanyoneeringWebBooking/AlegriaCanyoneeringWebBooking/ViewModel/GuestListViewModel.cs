using AlegriaCanyoneeringWebBooking.Models;
using System.ComponentModel.DataAnnotations;

namespace AlegriaCanyoneeringWebBooking.ViewModel
{
    public class GuestListViewModel
    {
        public Guest NewGuest { get; set; }  // required properties can stay
        public List<Guest> ReservedGuests { get; set; } = new List<Guest>();
        public string? BatchQrBase64 { get; set; }  // single QR for the whole batch
        public Dictionary<string, List<Guest>> BatchGuests { get; set; }
        public string BatchGuestsJson { get; set; }

    }
}
