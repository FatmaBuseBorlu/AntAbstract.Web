using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class Session
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required(ErrorMessage = "Oturum başlığı zorunludur.")]
        [StringLength(200, ErrorMessage = "Oturum başlığı en fazla 200 karakter olabilir.")]
        [Display(Name = "Oturum Başlığı")]
        public string Title { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "İngilizce oturum başlığı en fazla 200 karakter olabilir.")]
        [Display(Name = "İngilizce Oturum Başlığı")]
        public string? TitleEn { get; set; }

        [StringLength(2000, ErrorMessage = "Açıklama en fazla 2000 karakter olabilir.")]
        [Display(Name = "Açıklama")]
        public string? Description { get; set; }

        [StringLength(2000, ErrorMessage = "İngilizce açıklama en fazla 2000 karakter olabilir.")]
        [Display(Name = "İngilizce Açıklama")]
        public string? DescriptionEn { get; set; }

        [Required(ErrorMessage = "Oturum tarihi zorunludur.")]
        [DataType(DataType.Date)]
        [Display(Name = "Oturum Tarihi")]
        public DateTime SessionDate { get; set; }

        [Required(ErrorMessage = "Başlangıç saati zorunludur.")]
        [DataType(DataType.Time)]
        [Display(Name = "Başlangıç Saati")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "Bitiş saati zorunludur.")]
        [DataType(DataType.Time)]
        [Display(Name = "Bitiş Saati")]
        public TimeSpan EndTime { get; set; }

        [StringLength(100, ErrorMessage = "Salon / konum bilgisi en fazla 100 karakter olabilir.")]
        [Display(Name = "Salon / Konum")]
        public string? Location { get; set; }

        [StringLength(150, ErrorMessage = "Konuşmacı adı en fazla 150 karakter olabilir.")]
        [Display(Name = "Konuşmacı")]
        public string? SpeakerName { get; set; }

        [StringLength(250, ErrorMessage = "Sunum başlığı en fazla 250 karakter olabilir.")]
        [Display(Name = "Sunum / Bildiri Başlığı")]
        public string? PresentationTitle { get; set; }

        [StringLength(250, ErrorMessage = "İngilizce sunum başlığı en fazla 250 karakter olabilir.")]
        [Display(Name = "İngilizce Sunum / Bildiri Başlığı")]
        public string? PresentationTitleEn { get; set; }

        [Display(Name = "Sıralama")]
        public int SortOrder { get; set; } = 0;

        [Display(Name = "Aktif mi?")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Oluşturulma Tarihi")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Güncellenme Tarihi")]
        public DateTime? UpdatedDate { get; set; }

        [Required]
        [Display(Name = "Kongre")]
        public Guid ConferenceId { get; set; }

        [ForeignKey(nameof(ConferenceId))]
        public virtual Conference Conference { get; set; } = null!;

        public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}