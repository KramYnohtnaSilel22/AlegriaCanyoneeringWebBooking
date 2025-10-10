namespace AlegriaCanyoneeringWebBooking.ViewModel
{
    public class GuestPaginationViewModel
    {
        public List<GuestWithOperatorVM> Guests { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public string BatchFilter { get; set; }
    }

}
