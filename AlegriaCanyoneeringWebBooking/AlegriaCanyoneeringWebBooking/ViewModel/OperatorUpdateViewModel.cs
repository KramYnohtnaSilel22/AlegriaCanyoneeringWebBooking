using System.ComponentModel.DataAnnotations;

namespace AlegriaCanyoneeringWebBooking.ViewModel
{
    public class OperatorUpdateViewModel
    {
        [Required]
        public int OperatorId { get; set; }

        [Required, StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(255)]
        public string BusinessName { get; set; } = string.Empty;

        [Required]
        public int Age { get; set; }

        [Required, StringLength(255)]
        public string Gender { get; set; } = string.Empty;

        [Required, StringLength(255)]
        public string Username { get; set; } = string.Empty;

        [EmailAddress]
        public string? EmailAddress { get; set; }

        [Required]
        public int RoleId { get; set; }

        public string? RoleName { get; set; }
    }
}
