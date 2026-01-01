using AntAbstract.Domain.Entities;


namespace AntAbstract.Web.Models.ViewModels.Shared
{
    public class SelectConferenceViewModel
    {
        public string Title { get; set; } = "Kongre Seç";
        public string Lead { get; set; } = "";
        public string PostUrl { get; set; } = "";
        public string SubmitText { get; set; } = "Devam Et";

        public string? ReturnUrl { get; set; }
        public bool AutoRedirectIfSelected { get; set; } = true;

        public List<Conference> Conferences { get; set; } = new();
    }
}
