using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

// Namespace'i klasör yapınıza uygun hale getirdik
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

        [Required(ErrorMessage = "Bildiri dosyası yüklemelisiniz.")]
        [Display(Name = "Bildiri Dosyası (Word/PDF)")]
        public IFormFile SubmissionFile { get; set; }
    }
}