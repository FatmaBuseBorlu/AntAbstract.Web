using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic; // List için gerekli
using AntAbstract.Web.Models.ViewModels; // SubmissionAuthorViewModel için gerekli

namespace AntAbstract.Web.Models.ViewModels
{
    public class SubmissionCreateViewModel
    {
        [Required(ErrorMessage = "Bildiri başlığı zorunludur.")]
        [Display(Name = "Bildiri Başlığı")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Özet metni zorunludur.")]
        [Display(Name = "Özet Metni")]
        [StringLength(5000, ErrorMessage = "Özet çok uzun.")]
        public string AbstractText { get; set; }

        [Required(ErrorMessage = "Anahtar kelimeler zorunludur.")]
        [Display(Name = "Anahtar Kelimeler (Virgülle ayırın)")]
        public string Keywords { get; set; }

        [Display(Name = "Sunum Türü")]
        public int PresentationTypeId { get; set; }

        // --- HATA VEREN VE TEKRAR EDEN KISIM TEMİZLENDİ ---
        [Required(ErrorMessage = "Bildiri dosyası yüklemelisiniz.")]
        [Display(Name = "Bildiri Dosyası (Word/PDF)")]
        // SubmissionFile'ın tanımını buraya aldık.
        public IFormFile SubmissionFile { get; set; }

        // --- CONTROLLER'IN ARADIĞI ORTAK YAZARLAR LİSTESİ ---
        [Required(ErrorMessage = "En az bir yazar (siz dahil) olmalıdır.")]
        public List<SubmissionAuthorViewModel> Authors { get; set; } = new List<SubmissionAuthorViewModel>();

        // Bu ViewModel'in var olduğundan emin olun:
        // public SubmissionAuthorViewModel SubmissionAuthor { get; set; } 
    }
}