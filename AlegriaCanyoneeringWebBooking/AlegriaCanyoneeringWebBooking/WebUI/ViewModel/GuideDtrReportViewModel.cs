namespace AlegriaCanyoneeringWebBooking.WebUI.ViewModel
{
    public class GuideDtrReportViewModel
    {
        public string GuideName { get; set; } = "";
        public string Rfid { get; set; } = "";
        public int TripCount { get; set; }  // number of DTR records (trips)
        public int TotalGuests { get; set; } // sum of NoOfGuest
    }
}