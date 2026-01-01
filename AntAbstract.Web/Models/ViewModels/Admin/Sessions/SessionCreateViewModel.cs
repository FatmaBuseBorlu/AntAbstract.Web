using System;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Web.Models.ViewModels.Admin.Sessions
{
    public class SessionCreateViewModel
    {
        [Required]
        public string Slug { get; set; } = "";

        [Required]
        public Guid ConferenceId { get; set; }

        public string? ConferenceTitle { get; set; }

        [Required(ErrorMessage = "Oturum başlığı zorunludur.")]
        [StringLength(200)]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "Tarih ve saat zorunludur.")]
        public DateTime SessionDate { get; set; }

        [StringLength(200)]
        public string? Location { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
