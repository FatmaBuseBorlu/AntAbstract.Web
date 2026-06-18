using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public enum SponsorTier
    {
        Platinum = 1,
        Gold     = 2,
        Silver   = 3,
        Bronze   = 4,
        Partner  = 5,
        Media    = 6
    }

    public class Sponsor
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? LogoUrl { get; set; }

        [StringLength(200)]
        public string? WebsiteUrl { get; set; }

        public SponsorTier Tier { get; set; } = SponsorTier.Gold;

        public int SortOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public Guid ConferenceId { get; set; }

        [ForeignKey(nameof(ConferenceId))]
        public virtual Conference Conference { get; set; } = null!;
    }
}
