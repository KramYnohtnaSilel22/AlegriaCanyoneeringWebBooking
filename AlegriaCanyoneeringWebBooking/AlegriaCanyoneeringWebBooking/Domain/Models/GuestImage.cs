using AlegriaCanyoneeringWebBooking;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

[Table("tbl_guestimage")]
public class GuestImage
{
    [Key]

    [Column("imageid")]
    public int ImageId { get; set; }

    [Required]
    [Column("wristbondguestcode")]
    [StringLength(80)]
    public string WristbondGuestCode { get; set; } = null!;

    [Required]
    [Column("image")]
    public byte[] Image { get; set; } = null!;


}
