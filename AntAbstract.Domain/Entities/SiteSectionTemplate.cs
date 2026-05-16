using System;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Domain.Entities
{
    public class SiteSectionTemplate
    {
        public int Id { get; set; }

        [Required]
        public ConferencePageBlockType BlockType { get; set; }

        [Required]
        public int Order { get; set; }

        [Required]
        [StringLength(150)]
        public string NameTr { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string NameEn { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsDefault { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}