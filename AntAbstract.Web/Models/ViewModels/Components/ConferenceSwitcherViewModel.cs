using AntAbstract.Domain.Entities;

namespace AntAbstract.Web.Models.ViewModels.Components
{
    public class ConferenceSwitcherModel
    {
        public List<Conference> Conferences { get; set; } = new();
        public Guid? SelectedConferenceId { get; set; }
        public string? CurrentConferenceName { get; set; }
        public string ReturnUrl { get; set; } = "/Dashboard";
    }
}
