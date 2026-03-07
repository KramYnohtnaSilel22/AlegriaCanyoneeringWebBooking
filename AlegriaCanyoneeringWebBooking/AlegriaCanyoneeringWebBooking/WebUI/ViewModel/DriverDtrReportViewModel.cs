namespace AlegriaCanyoneeringWebBooking.WebUI.ViewModel
{
    public class DriverDtrReportViewModel
    {
        public string DriverName { get; set; } = "";
        public string RefId { get; set; } = "";
        public int TripCount { get; set; }   // how many DTR records (trips)
        public int TotalPassenger { get; set; }  // total passengers across all trips
    }
}