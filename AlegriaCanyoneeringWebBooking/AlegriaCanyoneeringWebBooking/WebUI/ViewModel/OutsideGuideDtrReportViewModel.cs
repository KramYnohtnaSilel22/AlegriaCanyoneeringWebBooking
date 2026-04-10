namespace AlegriaCanyoneeringWebBooking.WebUI.ViewModel
{
    public class OutsideGuideDtrReportViewModel
    {
        public string Rfid { get; set; } = string.Empty;
        public string GuideName { get; set; } = string.Empty;
        public string? Nickname { get; set; }
        public string Address { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public int TripCount { get; set; }       // Number of jumps (guest count)
        public int TotalGuests { get; set; }
        public decimal RatePerJump { get; set; }
        public decimal GrossSalary { get; set; }
        public decimal NetPay { get; set; }
        public string Area { get; set; } = string.Empty;
        public string PlaceOfIssue { get; set; } = string.Empty;
        public int? OperatorId { get; set; }
        public string? OperatorName { get; set; }

        /// <summary>Display: "LASTNAME, FIRSTNAME [NICKNAME]"</summary>
        public string DisplayLabel =>
            !string.IsNullOrWhiteSpace(Nickname)
                ? $"{GuideName} ({Nickname})"
                : GuideName;
    }
}