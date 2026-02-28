using System.Collections.Generic;

namespace AntAbstract.Web.Models.WebsiteBlocks
{
    public class AboutBlockContent
    {
        public string Description { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public List<string> Features { get; set; } = new List<string>();
    }

    public class FaqBlockContent
    {
        public List<FaqItem> Questions { get; set; } = new List<FaqItem>();
    }
    public class FaqItem
    {
        public string Question { get; set; } = "";
        public string Answer { get; set; } = "";
    }

    public class SponsorBlockContent
    {
        public List<SponsorItem> Sponsors { get; set; } = new List<SponsorItem>();
    }
    public class SponsorItem
    {
        public string Name { get; set; } = "";
        public string LogoUrl { get; set; } = "";
        public string WebsiteUrl { get; set; } = "";
        public string Tier { get; set; } = "Gold";
    }
}