using AntAbstract.Domain.Entities;

namespace AntAbstract.Web.Models.ViewModels.Website
{
    public class ConferenceBlockRenderViewModel
    {
        public Conference Conference { get; set; } = null!;
        public ConferencePageBlock Block { get; set; } = null!;
    }
}
