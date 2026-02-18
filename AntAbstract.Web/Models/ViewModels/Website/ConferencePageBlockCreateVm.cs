using AntAbstract.Domain.Entities;
using AntAbstract.Web.Models.ViewModels.Website.Blocks;

namespace AntAbstract.Web.Models.ViewModels.Website
{
    public class ConferencePageBlockCreateVm
    {
        public Guid TenantId { get; set; }
        public Guid ConferenceId { get; set; }
        public string Page { get; set; } = "Home";
        public string Culture { get; set; } = "tr-TR";

        public ConferencePageBlockType BlockType { get; set; }

        public string? Title { get; set; }
        public string? Subtitle { get; set; }

        public int Order { get; set; }
        public bool IsActive { get; set; } = true;

        public HeroContent Hero { get; set; } = new HeroContent();
    }
}
