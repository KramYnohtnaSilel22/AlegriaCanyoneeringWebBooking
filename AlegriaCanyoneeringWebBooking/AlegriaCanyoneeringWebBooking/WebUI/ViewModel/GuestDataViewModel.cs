using System.ComponentModel.DataAnnotations;

namespace AlegriaCanyoneeringWebBooking
{
    public class GuestDataViewModel
    {
        [Required]
        [DataType(DataType.Date)]
        public string StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public string EndDate { get; set; }

        public int Draw { get; set; }
        public int Start { get; set; }
        public int Length { get; set; }
        public string SearchValue { get; set; }
    }

}
