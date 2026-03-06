using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    /// <summary>
    /// Stores the assignment of guides and drivers to a batch, linked to the operator.
    /// Table: tbl_batch_assignments
    /// </summary>
    [Table("tbl_batch_assignments")]
    public class BatchAssignment
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        // ─────────────────────────────────────────
        // Batch — string FK to Guest.Batch
        // No separate Batch table exists, so stored
        // as a plain string matching Guest.Batch
        // ─────────────────────────────────────────
        [Required]
        [Column("batch_code")]
        [StringLength(100)]
        public string BatchCode { get; set; } = string.Empty;

        // ─────────────────────────────────────────
        // FK → tbl_operator_mobile.Id
        // ─────────────────────────────────────────
        [Column("operator_id")]
        public int? OperatorId { get; set; }

        [ForeignKey(nameof(OperatorId))]
        public Operator? Operator { get; set; }

        // ─────────────────────────────────────────
        // FK → tourguide_details.id
        // ─────────────────────────────────────────
        [Column("guide_id")]
        public int? GuideId { get; set; }

        [ForeignKey(nameof(GuideId))]
        public Guide? Guide { get; set; }

        // ─────────────────────────────────────────
        // FK → driver_details.id
        // ─────────────────────────────────────────
        [Column("driver_id")]
        public int? DriverId { get; set; }

        [ForeignKey(nameof(DriverId))]
        public Driver? Driver { get; set; }

      
    }
}