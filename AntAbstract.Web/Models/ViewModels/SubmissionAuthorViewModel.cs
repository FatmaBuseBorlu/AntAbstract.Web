using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Web.Models.ViewModels
{
    // Bu sınıf, SubmissionCreateViewModel'in içindeki yazar listesi için gereklidir.
    public class SubmissionAuthorViewModel
    {
        // Hata listesindeki 6 alanın tamamı buraya eklenmiştir:

        [Required(ErrorMessage = "Adı zorunludur.")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Soyadı zorunludur.")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Kurum zorunludur.")]
        public string Institution { get; set; } // Hata listesindeki Institution

        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        public string Email { get; set; } // Hata listesindeki Email

        public string? ORCID { get; set; } // Hata listesindeki ORCID

        // Controller'daki mantık için gerekli:
        public bool IsCorrespondingAuthor { get; set; } // Hata listesindeki IsCorrespondingAuthor
        public int Order { get; set; }
    }
}