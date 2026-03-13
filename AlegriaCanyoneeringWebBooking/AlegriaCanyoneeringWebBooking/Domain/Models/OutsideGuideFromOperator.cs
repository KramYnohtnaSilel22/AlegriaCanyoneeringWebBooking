using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.WebUI.Models
{
    [Table("outside_guide_from_operator")]
    public class OutsideGuideFromOperator
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

        // Guide.Rfid
        [Required]
        [Display(Name = "Guide ID")]
        [StringLength(100)]
        public string GuideId { get; set; } = "";

        // Guide.FName + MName + LName (full name)
        [Required]
        [Display(Name = "Guide Name")]
        [StringLength(200)]
        public string GuideName { get; set; } = "";
    }
}