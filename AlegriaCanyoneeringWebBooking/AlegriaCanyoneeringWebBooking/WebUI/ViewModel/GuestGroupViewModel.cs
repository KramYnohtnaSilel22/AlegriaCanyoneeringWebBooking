
using AlegriaCanyoneeringWebBooking.Models;


namespace AlegriaCanyoneeringWebBooking
{
    public class GuestGroupViewModel
    {
        public int Id { get; set; } // The real guest ID (first guest in batch)
        public int? OperatorId { get; set; }
        public Operator Operator { get; set; }
        public int ActiveGuestCount { get; set; }
        public string ArrivalDate { get; set; }
        public string Date { get; set; }
        public string BookingStatus { get; set; }
        public List<int> GuestIds { get; set; } = new();
        public string Batch { get; set; }

        public string? BatchQrBase64 { get; set; }
    }
}
