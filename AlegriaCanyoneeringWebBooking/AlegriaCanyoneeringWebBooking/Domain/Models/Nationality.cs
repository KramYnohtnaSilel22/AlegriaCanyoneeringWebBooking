using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models;

[Table("nationalities")]
public class Nationality
{
    [Key]
    [Column("id")]
    public int id { get; set; }

    [Required, MaxLength(1000)]
    [Display(Name = "Nationality")]
    [Column("nat_name")]
    public string NatName { get; set; }

}
