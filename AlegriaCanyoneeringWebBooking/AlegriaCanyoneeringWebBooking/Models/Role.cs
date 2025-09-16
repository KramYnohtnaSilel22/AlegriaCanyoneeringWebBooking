using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("role")]
    public class Role
    {
        [Key]
        [Column("RoleId")]
        public int RoleId { get; set; }

        [Required]
        [StringLength(255)]
        [Column("Name")]
        public string Name { get; set; }
    }
}
