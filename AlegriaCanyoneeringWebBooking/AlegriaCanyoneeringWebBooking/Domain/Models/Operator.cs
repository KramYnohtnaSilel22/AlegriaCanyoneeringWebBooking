using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("tbl_operator_mobile")]
    public class Operator
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Required, StringLength(255)]
        [Column("Name")]
        public string? Name { get; set; }


        [Column("BusinessName")]
        [Display(Name = "Business Name")]
        public string? BusinessName { get; set; } = string.Empty;

        [Required]
        [Column("Age")]
        public int Age { get; set; }

        [Required, StringLength(255)]
        [Column("Gender")]
        public string? Gender { get; set; }

        [Required, StringLength(255)]
        [Column("Username")]
        public string? Username { get; set; }

        [StringLength(255)]
        [DataType(DataType.Password)]   
        public string? Password { get; set; }

        [Column("EmailAddress")]
        [DataType(DataType.EmailAddress)]
        [Display(Name = "Email Address")]
        public string EmailAddress { get; set; }

        [Required]
        [Column("Role")]
        public int RoleId { get; set; }

        [ForeignKey(nameof(RoleId))]
        public Role? Roles { get; set; }

        // Navigation property for related Guests
        // Navigation collection to Guests (optional, but good for reverse lookup)
        public ICollection<Guest>? Guests { get; set; }

   
    }
}
