using AntAbstract.Domain.Entities;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels.Website
{
    public class ConferenceHomePageViewModel
    {
        public Conference Conference { get; set; } = null!;
        public List<ConferencePageBlock> Blocks { get; set; } = new();
        public string Culture { get; set; } = "tr-TR";
        public string Page { get; set; } = "Home";
        public List<Conference> SuggestedConferences { get; set; } = new();
    }
}
