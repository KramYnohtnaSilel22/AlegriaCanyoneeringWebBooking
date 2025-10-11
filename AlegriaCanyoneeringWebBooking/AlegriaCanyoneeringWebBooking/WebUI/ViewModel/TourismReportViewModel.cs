namespace AlegriaCanyoneeringWebBooking
{
    public class TourismReportViewModel
    {
        public string Label { get; set; } = string.Empty;

        // This Province
        public int ThisProvinceMale { get; set; }
        public int ThisProvinceFemale { get; set; }

        // Other Province
        public int OtherProvinceMale { get; set; }
        public int OtherProvinceFemale { get; set; }

        public int Total => OtherProvinceMale + OtherProvinceFemale;

        // Foreign Country Residence
        public int ForeignMale { get; set; }
        public int ForeignFemale { get; set; }

        // Grand Total
        public int TotalMale => ThisProvinceMale + OtherProvinceMale + ForeignMale;
        public int TotalFemale => ThisProvinceFemale + OtherProvinceFemale + ForeignFemale;
        public int GrandTotal => TotalMale + TotalFemale;


    }
}
