using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Web.Models.ViewModels
{
 
    public class SubmissionAuthorViewModel
    {


        [Required(ErrorMessage = "Adı zorunludur.")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Soyadı zorunludur.")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Kurum zorunludur.")]
        public string Institution { get; set; } 

        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        public string Email { get; set; } 

        public string? ORCID { get; set; } 

        public bool IsCorrespondingAuthor { get; set; } 
        public int Order { get; set; }
    }
}