using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.WebUI.Models
{
    [Table("outside_guide_from_operator")]
    public class OutsideGuideFromOperator
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Operator ID")]
        [StringLength(100)]
        [Column("operator_id")]
        public string OperatorId { get; set; } = "";

        [Required]
        [Display(Name = "Operator Name")]
        [StringLength(200)]
        [Column("operator_name")]
        public string OperatorName { get; set; } = "";

        [Required]
        [Display(Name = "Guide ID")]
        [StringLength(100)]
        [Column("outsideguide_id")]
        public string OutsideGuideId { get; set; } = "";

        [Required]
        [Display(Name = "Guide Name")]
        [StringLength(200)]
        [Column("outsideguide_name")]
        public string OutsideGuideName { get; set; } = "";
    }
}