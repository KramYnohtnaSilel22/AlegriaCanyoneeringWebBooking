using System.ComponentModel.DataAnnotations;

namespace AlegriaCanyoneeringWebBooking
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
