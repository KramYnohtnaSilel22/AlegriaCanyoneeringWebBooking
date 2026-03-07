namespace AlegriaCanyoneeringWebBooking.WebUI.ViewModel
{
    public class DriverAttendanceReportViewModel
    {
        public string DriverName { get; set; } = "";
        public string RefId { get; set; } = "";
        public string Date { get; set; } = "";   // display date string
        public int Passenger { get; set; }
    }
}