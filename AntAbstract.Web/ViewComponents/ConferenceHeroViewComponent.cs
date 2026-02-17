using AntAbstract.Domain.Entities;
using AntAbstract.Web.Models.WebsiteBlocks;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AntAbstract.Web.ViewComponents
{
    public class ConferenceHeroViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(ConferencePageBlock block)
        {
            HeroBlockContent content = new();

            if (!string.IsNullOrWhiteSpace(block.ContentJson))
            {
                try
                {
                    content = JsonSerializer.Deserialize<HeroBlockContent>(block.ContentJson) ?? new HeroBlockContent();
                }
                catch
                {
                    content = new HeroBlockContent();
                }
            }

            ViewBag.Content = content;
            return View(block);
        }
    }
}
