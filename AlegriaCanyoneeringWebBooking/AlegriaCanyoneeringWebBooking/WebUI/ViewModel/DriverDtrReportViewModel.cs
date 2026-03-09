namespace AlegriaCanyoneeringWebBooking.WebUI.ViewModel
{
    public class DriverDtrReportViewModel
    {
        public string DriverName { get; set; } = "";
        public string RefId { get; set; } = "";
        public string Address { get; set; } = "Alegria, Cebu";
        public string Designation { get; set; } = "Driver";
        public int TripCount { get; set; }   // how many DTR records
        public int TotalPassenger { get; set; }   // total passengers
        public int RatePerDay { get; set; } = 100;
        public decimal GrossSalary { get; set; }   // TotalPassenger × RatePerDay
        public decimal NetPay { get; set; }   // = GrossSalary
        public string PlaceOfIssue { get; set; } = "Alegria, Cebu";
    }
}