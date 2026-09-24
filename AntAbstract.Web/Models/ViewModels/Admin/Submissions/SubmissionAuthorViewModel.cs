using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Web.Models.ViewModels.Admin.Submissions
{
    /// <summary>
    /// Yazar alanlarında uzunluk sınırı tanımlı değildi. Veritabanı sütunları
    /// sınırlı olduğu için sınırı aşan bir değer kaydetme anında patlıyor ve
    /// kullanıcıya "An error occurred while saving the entity changes" gibi
    /// hiçbir şey anlatmayan bir mesaj dönüyordu. En sık ORCID'de oluyordu:
    /// insanlar tam adresi (https://orcid.org/0000-...) yapıştırıyor, sütun ise
    /// 50 karakter.
    ///
    /// Sınırlar veritabanındaki sütun genişlikleriyle birebir aynı tutulmalı;
    /// biri değişirse diğeri de değişmeli.
    /// </summary>
    public class SubmissionAuthorViewModel
    {
        [Required(ErrorMessage = "Adı zorunludur.")]
        [StringLength(100, ErrorMessage = "Ad en fazla 100 karakter olabilir.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Soyadı zorunludur.")]
        [StringLength(100, ErrorMessage = "Soyad en fazla 100 karakter olabilir.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kurum zorunludur.")]
        [StringLength(200, ErrorMessage = "Kurum en fazla 200 karakter olabilir.")]
        public string Institution { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [StringLength(200, ErrorMessage = "E-posta en fazla 200 karakter olabilir.")]
        public string Email { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "ORCID en fazla 50 karakter olabilir.")]
        public string? ORCID { get; set; }

        public bool IsCorrespondingAuthor { get; set; }
        public int Order { get; set; }
    }
}
