using AntAbstract.Domain.Entities;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels.Website
{
    public class ConferenceHomePageViewModel
    {
        public Conference Conference { get; set; } = null!;
        public List<ConferencePageBlock> Blocks { get; set; } = new();
    }
}
