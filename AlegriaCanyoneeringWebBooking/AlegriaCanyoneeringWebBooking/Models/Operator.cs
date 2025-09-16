using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("operator")]
    public class Operator
    {
        [Key]
        [Column("OperatorId")]
        public int OperatorId { get; set; }

        [Required, StringLength(255)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(255)]
        [Column("BusinessName")]
        public string BusinessName { get; set; } = string.Empty;

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
        [Required]
        [Column("RoleId")]
        public int RoleId { get; set; }

        [ForeignKey(nameof(RoleId))]
        public Role? Roles { get; set; }
    }
}
