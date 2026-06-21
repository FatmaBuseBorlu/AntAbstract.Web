using System;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Web.Models.ViewModels.Admin.Sessions
{
    public class SessionCreateViewModel
    {
        [Required]
        public string Slug { get; set; } = string.Empty;

        [Required]
        public Guid ConferenceId { get; set; }

        public string? ConferenceTitle { get; set; }

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
        public DateTime SessionDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Başlangıç saati zorunludur.")]
        [DataType(DataType.Time)]
        [Display(Name = "Başlangıç Saati")]
        public TimeSpan StartTime { get; set; } = new TimeSpan(9, 0, 0);

        [Required(ErrorMessage = "Bitiş saati zorunludur.")]
        [DataType(DataType.Time)]
        [Display(Name = "Bitiş Saati")]
        public TimeSpan EndTime { get; set; } = new TimeSpan(10, 0, 0);

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

        [StringLength(500)]
        [Display(Name = "Canlı Yayın Linki")]
        public string? LiveStreamUrl { get; set; }

        [StringLength(50)]
        [Display(Name = "Yayın Platformu")]
        public string? LiveStreamPlatform { get; set; }

        [Display(Name = "Sıralama")]
        public int SortOrder { get; set; } = 0;

        [Display(Name = "Aktif mi?")]
        public bool IsActive { get; set; } = true;

        public string? ReturnUrl { get; set; }
    }
}