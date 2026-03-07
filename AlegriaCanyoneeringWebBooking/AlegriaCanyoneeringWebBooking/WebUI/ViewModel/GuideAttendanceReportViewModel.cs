namespace AlegriaCanyoneeringWebBooking.WebUI.ViewModel
{
    public class GuideAttendanceReportViewModel
    {
        public string GuideName { get; set; } = "";
        public string Rfid { get; set; } = "";
        public string Date { get; set; } = "";   // display date string
        public int Guests { get; set; }
    }
}