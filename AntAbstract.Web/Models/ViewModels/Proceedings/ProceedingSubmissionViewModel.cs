using System;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels.Proceedings
{
    public sealed class ProceedingSubmissionViewModel
    {
        public string Slug { get; set; } = "";

        public string SubmissionIdCode { get; set; } = "";

        public string Title { get; set; } = "";

        public string Abstract { get; set; } = "";

        public string Keywords { get; set; } = "";

        public string Topic { get; set; } = "";

        public string PresentationType { get; set; } = "";

        public string ConferenceTitle { get; set; } = "";

        public DateTime ConferenceStartDate { get; set; }

        public DateTime ConferenceEndDate { get; set; }

        public string? DoiUrl { get; set; }

        public List<string> Authors { get; set; } = new();
    }
}
