using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.WebUI.Models
{
    [Table("outside_driver_from_operator")]
    public class OutsideDriverFromOperator
    {
        [Key]
        public int Id { get; set; }

        // Operator.Id (stored as string)
        [Required]
        [Display(Name = "Operator ID")]
        [StringLength(100)]
        public string OperatorId { get; set; } = "";

        // Operator.BusinessName
        [Required]
        [Display(Name = "Operator Name")]
        [StringLength(200)]
        public string OperatorName { get; set; } = "";

        // Driver.Rfid
        [Required]
        [Display(Name = "Driver ID")]
        [StringLength(100)]
        public string DriverId { get; set; } = "";

        // Driver.FName + MName + LName (full name)
        [Required]
        [Display(Name = "Driver Name")]
        [StringLength(200)]
        public string DriverName { get; set; } = "";
    }
}