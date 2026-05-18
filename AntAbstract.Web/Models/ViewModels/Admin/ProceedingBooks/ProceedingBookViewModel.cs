using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Web.Models.ViewModels.Admin.ProceedingBooks
{
    public class ProceedingBookViewModel
    {
        [Required]
        public Guid ConferenceId { get; set; }

        public string Slug { get; set; } = string.Empty;

        public string ConferenceTitle { get; set; } = string.Empty;

        [Display(Name = "Bildiri Kitabı Yayında mı?")]
        public bool IsProceedingBookPublished { get; set; }

        [Display(Name = "Mevcut Bildiri Kitabı")]
        public string? ProceedingBookFilePath { get; set; }

        [Display(Name = "Yayınlanma Tarihi")]
        public DateTime? ProceedingBookPublishedDate { get; set; }

        [Display(Name = "Yeni PDF Dosyası")]
        public IFormFile? ProceedingBookFile { get; set; }

        public string? ReturnUrl { get; set; }
    }
}