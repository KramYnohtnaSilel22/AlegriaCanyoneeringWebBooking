using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlegriaCanyoneeringWebBooking.Models
{
    [Table("outside_tourguide_details")]
    public class OutsideGuide
    {
        [Key]
        [Column("id")]
        public int OutsideGuideId { get; set; }

        [Required]
        [Column("rfid")]
        [StringLength(200)]
        public string Rfid { get; set; } = string.Empty;

        [Required]
        [Column("tposition")]
        public int TPosition { get; set; }

        [Required]
        [Column("fname")]
        [StringLength(100)]
        public string FName { get; set; } = string.Empty;

        [Column("mname")]
        [StringLength(100)]
        public string? MName { get; set; }

        [Required]
        [Column("lname")]
        [StringLength(100)]
        public string LName { get; set; } = string.Empty;

        [Required]
        [Column("cnumber")]
        [StringLength(20)]
        public string CNumber { get; set; } = string.Empty;

        [Required]
        [Column("address")]
        [StringLength(255)]
        public string Address { get; set; } = string.Empty;

        [Column("nickname")]
        [StringLength(100)]
        public string? Nickname { get; set; }

        [Column("image")]
        [StringLength(255)]
        public string? Image { get; set; }

        [Column("operatorid")]
        public int? OperatorId { get; set; }

        // ── Navigation ────────────────────────────────────────────────
        [ForeignKey(nameof(OperatorId))]
        public Operator? Operator { get; set; }

        // ── Computed — never hits the DB ──────────────────────────────
        [NotMapped]
        public string FullName =>
            string.IsNullOrWhiteSpace(MName)
                ? $"{FName} {LName}".Trim()
                : $"{FName} {MName} {LName}".Trim();

        [NotMapped]
        public string DisplayName =>
            !string.IsNullOrWhiteSpace(Nickname)
                ? $"{FullName} ({Nickname})"
                : FullName;
    }
}