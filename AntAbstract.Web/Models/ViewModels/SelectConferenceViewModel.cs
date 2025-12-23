using AntAbstract.Domain.Entities;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class SelectConferenceViewModel
    {
        public string Title { get; set; } = "Kongre Seç";
        public string Lead { get; set; } = "";
        public string PostUrl { get; set; } = "";
        public string SubmitText { get; set; } = "Devam Et";
        public List<Conference> Conferences { get; set; } = new();
    }
}
