namespace AlegriaCanyoneeringWebBooking.WebUI.ViewModel
{
    public class DriverQueueItem
    {
        public int DriverId { get; set; }
        public string RefId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Image { get; set; }
        public int DPosition { get; set; }
    }
}