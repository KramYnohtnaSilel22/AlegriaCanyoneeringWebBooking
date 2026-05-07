using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("tbl_batch_assignments")]
    public class BatchAssignment
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("batch_code")]
        [StringLength(100)]
        public string BatchCode { get; set; } = string.Empty;

        // FK → tbl_operator_mobile.Id
        [Column("operator_id")]
        public int? OperatorId { get; set; }

        [ForeignKey(nameof(OperatorId))]
        public Operator? Operator { get; set; }

        // FK → internal guide
        [Column("guide_id")]
        public int? GuideId { get; set; }

        [ForeignKey(nameof(GuideId))]
        public Guide? Guide { get; set; }

        // FK → outside guide
        [Column("outside_guide_id")]
        public int? OutsideGuideId { get; set; }

        [ForeignKey(nameof(OutsideGuideId))]
        public OutsideGuide? OutsideGuide { get; set; }

        // FK → driver
        [Column("driver_id")]
        public int? DriverId { get; set; }

        [ForeignKey(nameof(DriverId))]
        public Driver? Driver { get; set; }


        [NotMapped]
        public string AssignedGuideName =>
            OutsideGuide != null
                ? OutsideGuide.DisplayName
                : Guide != null
                    ? $"{Guide.FName} {Guide.LName}"
                    : "No Guide Assigned";
    }
}