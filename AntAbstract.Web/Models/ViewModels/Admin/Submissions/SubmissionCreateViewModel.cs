using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Web.Models.ViewModels.Admin.Submissions
{
    public class SubmissionCreateViewModel
    {
        [Required(ErrorMessage = "Bildiri başlığı zorunludur.")]
        [Display(Name = "Bildiri Başlığı")]
        [MaxLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Özet metni zorunludur.")]
        [Display(Name = "Özet Metni")]
        [StringLength(5000, ErrorMessage = "Özet çok uzun. Lütfen kısaltınız.")]
        public string AbstractText { get; set; } = string.Empty;

        [Required(ErrorMessage = "Anahtar kelimeler zorunludur.")]
        [Display(Name = "Anahtar Kelimeler")]
        public string Keywords { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lütfen bildiri konusunu/temasını seçiniz.")]
        [Display(Name = "Bildiri Konusu / Tema")]
        public Guid? ConferenceTopicId { get; set; }

        [MaxLength(150)]
        public string? Topic { get; set; }

        [Required(ErrorMessage = "Lütfen sunum türünü seçiniz.")]
        [Display(Name = "Sunum Türü")]
        [MaxLength(50)]
        public string PresentationType { get; set; } = string.Empty;

        [Display(Name = "Bildiri Dosyası")]
        public IFormFile? SubmissionFile { get; set; }

        public List<SubmissionAuthorViewModel> Authors { get; set; } = new();

        [Required(ErrorMessage = "Lütfen başvuru yapılacak kongreyi seçiniz.")]
        [Display(Name = "Başvuru Yapılacak Kongre")]
        public Guid ConferenceId { get; set; }

        public List<SelectListItem> AvailableConferences { get; set; } = new();

        public List<SelectListItem> AvailableTopics { get; set; } = new();

        public List<SelectListItem> PresentationTypes { get; set; } = new()
        {
            new SelectListItem { Value = "Oral", Text = "Sözlü Sunum" },
            new SelectListItem { Value = "Poster", Text = "Poster Sunum" },
            new SelectListItem { Value = "Online", Text = "Çevrimiçi Sunum" }
        };
    }
}