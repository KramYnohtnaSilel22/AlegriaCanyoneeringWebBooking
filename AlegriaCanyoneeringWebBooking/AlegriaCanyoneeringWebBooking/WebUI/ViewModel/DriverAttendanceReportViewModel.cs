namespace AlegriaCanyoneeringWebBooking.WebUI.ViewModel
{
    public class DriverAttendanceReportViewModel
    {
        public string DriverName { get; set; } = "";
        public string RefId { get; set; } = "";
        public string Date { get; set; } = "";   // display date string
        public string Time { get; set; } = "";   // extracted from unix timestamp
        public int Passenger { get; set; }
        public string Route { get; set; } = "";   // Guest.Area via BatchAssignment
    }
}