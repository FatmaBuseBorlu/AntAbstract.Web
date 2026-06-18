using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Web.Models.ViewModels.Admin.Referee
{
    public class RefereeCreateViewModel
    {
        [Required(ErrorMessage = "Ad zorunludur")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Soyad zorunludur")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email zorunludur")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kurum zorunludur")]
        public string Institution { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur")]
        public string Password { get; set; } = string.Empty;
    }
}
