namespace AlegriaCanyoneeringWebBooking.WebUI.ViewModel
{
    public class GuideDtrReportViewModel
    {
        public string GuideName { get; set; } = "";
        public string Rfid { get; set; } = "";
        public string Address { get; set; } = "Alegria, Cebu";
        public string Designation { get; set; } = "Guide";
        public int TripCount { get; set; }   // jumps / trips
        public int TotalGuests { get; set; }   // total guests across trips
        public int RatePerJump { get; set; }   // Wonder Falls=500, Kawasan Exit=600, Kanlaob=0
        public decimal GrossSalary { get; set; }   // TripCount × RatePerJump
        public decimal NetPay { get; set; }   // = GrossSalary
        public string PlaceOfIssue { get; set; } = "Alegria, Cebu";
        public string Area { get; set; } = "";
    }
}