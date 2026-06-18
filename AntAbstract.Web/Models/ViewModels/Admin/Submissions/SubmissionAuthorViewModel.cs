using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Web.Models.ViewModels.Admin.Submissions
{

    public class SubmissionAuthorViewModel
    {


        [Required(ErrorMessage = "Adı zorunludur.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Soyadı zorunludur.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kurum zorunludur.")]
        public string Institution { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        public string Email { get; set; } = string.Empty;

        public string? ORCID { get; set; }

        public bool IsCorrespondingAuthor { get; set; }
        public int Order { get; set; }
    }
}
