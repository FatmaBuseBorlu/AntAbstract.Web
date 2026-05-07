using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class ConferenceTopic
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ConferenceId { get; set; }

        [ForeignKey(nameof(ConferenceId))]
        public virtual Conference Conference { get; set; } = null!;

        [Required(ErrorMessage = "Konu adı zorunludur.")]
        [StringLength(150, ErrorMessage = "Konu adı en fazla 150 karakter olabilir.")]
        [Display(Name = "Konu Adı")]
        public string Name { get; set; } = string.Empty;

        [StringLength(150, ErrorMessage = "İngilizce konu adı en fazla 150 karakter olabilir.")]
        [Display(Name = "İngilizce Konu Adı")]
        public string? NameEn { get; set; }

        [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
        [Display(Name = "Açıklama")]
        public string? Description { get; set; }

        [StringLength(500, ErrorMessage = "İngilizce açıklama en fazla 500 karakter olabilir.")]
        [Display(Name = "İngilizce Açıklama")]
        public string? DescriptionEn { get; set; }

        [Display(Name = "Aktif mi?")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Sıralama")]
        public int SortOrder { get; set; } = 0;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}