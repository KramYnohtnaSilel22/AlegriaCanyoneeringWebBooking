namespace AlegriaCanyoneeringWebBooking.WebUI.ViewModel
{
    public class GuideAttendanceReportViewModel
    {
        public string GuideName { get; set; } = "";
        public string Rfid { get; set; } = "";
        public string Date { get; set; } = "";   // display date string
        public string Time { get; set; } = "";   // extracted from unix timestamp
        public int Guests { get; set; }
        public string Route { get; set; } = "";   // Guest.Area via BatchAssignment
    }
}