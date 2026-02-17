using AntAbstract.Domain.Common;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class ConferencePageBlock : IMustHaveTenant
    {
        public int Id { get; set; }

        public Guid TenantId { get; set; }

        public Guid ConferenceId { get; set; }

        [ForeignKey(nameof(ConferenceId))]
        public Conference Conference { get; set; } = null!;

        [Required, StringLength(30)]
        public string Page { get; set; } = "Home";

        [Required, StringLength(10)]
        public string Culture { get; set; } = "tr-TR";

        public ConferencePageBlockType BlockType { get; set; }

        [StringLength(200)]
        public string? Title { get; set; }

        [StringLength(400)]
        public string? Subtitle { get; set; }

        public string? ContentJson { get; set; }

        public int Order { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
