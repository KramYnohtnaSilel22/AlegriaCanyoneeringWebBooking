

using AlegriaCanyoneeringWebBooking.Models;

namespace AlegriaCanyoneeringWebBooking
{
    public class FinalBookingViewModel
    {
        public Guest Guest { get; set; }
        public string Fullname { get; set; }
        public string BookingStatus { get; set; }
        public string OperatorName { get; set; }
        public string Nationality { get; set; }
        public string Batch { get; set; }
        public int NumberOfGuests { get; set; }
        public string BookingDate { get; set; }
        public string ArrivalDate { get; set; }
        public string ContactNumber { get; set; }
        public string QRCodeBase64 { get; set; }
        public string AdditionalNotes { get; set; }
    }


}
