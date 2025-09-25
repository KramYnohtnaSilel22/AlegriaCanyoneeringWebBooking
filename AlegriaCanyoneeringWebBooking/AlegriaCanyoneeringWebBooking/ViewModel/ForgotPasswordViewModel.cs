using System.ComponentModel.DataAnnotations;

namespace AlegriaCanyoneeringWebBooking.ViewModel
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
