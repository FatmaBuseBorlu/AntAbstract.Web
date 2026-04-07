using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AntAbstract.Web.Models.ViewModels.Admin.Submissions
{
    public class SubmissionCreateViewModel
    {
        [Required(ErrorMessage = "Bildiri başlığı zorunludur.")]
        [Display(Name = "Bildiri Başlığı")]
        [MaxLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir.")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Özet metni zorunludur.")]
        [Display(Name = "Özet Metni")]
        [StringLength(5000, ErrorMessage = "Özet çok uzun. Lütfen kısaltınız.")]
        public string AbstractText { get; set; }

        [Required(ErrorMessage = "Anahtar kelimeler zorunludur.")]
        [Display(Name = "Anahtar Kelimeler (Virgülle ayırın)")]
        public string Keywords { get; set; }

        [Required(ErrorMessage = "Lütfen bildiri konusunu/temasını seçiniz.")]
        [Display(Name = "Bildiri Konusu (Kategori)")]
        [MaxLength(100)]
        public string Topic { get; set; }
     
        [Required(ErrorMessage = "Lütfen sunum türünü seçiniz.")]
        [Display(Name = "Sunum Türü")]
        [MaxLength(50)]
        public string PresentationType { get; set; }

        [Display(Name = "Bildiri Dosyası (Word/PDF)")]
        public IFormFile SubmissionFile { get; set; }

        [Required(ErrorMessage = "En az bir yazar (siz dahil) olmalıdır.")]
        public List<SubmissionAuthorViewModel> Authors { get; set; } = new List<SubmissionAuthorViewModel>();

        [Required(ErrorMessage = "Lütfen başvuru yapılacak kongreyi seçiniz.")]
        [Display(Name = "Başvuru Yapılacak Kongre")]
        public Guid ConferenceId { get; set; }

        public List<SelectListItem> AvailableConferences { get; set; } = new List<SelectListItem>();

        public List<SelectListItem> PresentationTypes { get; set; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "Sözlü Sunum", Text = "Sözlü Sunum" },
            new SelectListItem { Value = "Poster Sunum", Text = "Poster Sunum" },
            new SelectListItem { Value = "Çevrimiçi (Online) Sunum", Text = "Çevrimiçi (Online) Sunum" }
        };
    }
}