namespace AlegriaCanyoneeringWebBooking.WebUI.ViewModel
{
    public class OutsideGuideQueueItem
    {
        public int OutsideGuideId { get; set; }
        public string? Rfid { get; set; }
        public string FullName { get; set; } = "";
        public string? Image { get; set; }
        public int TPosition { get; set; }
    }
}
