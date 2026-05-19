using System;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels.Proceedings
{
    public class ProceedingBookPageViewModel
    {
        public Guid ConferenceId { get; set; }

        public string Slug { get; set; } = "";

        public string ConferenceTitle { get; set; } = "";

        public string? ProceedingBookFilePath { get; set; }

        public bool IsProceedingBookPublished { get; set; }

        public DateTime? ProceedingBookPublishedDate { get; set; }

        public bool IsSingleConferencePage { get; set; }

        public List<ProceedingBookItemViewModel> Books { get; set; } = new();
    }

    public class ProceedingBookItemViewModel
    {
        public Guid ConferenceId { get; set; }

        public string ConferenceTitle { get; set; } = "";

        public string Slug { get; set; } = "";

        public string FileUrl { get; set; } = "";

        public string DownloadUrl { get; set; } = "";

        public int Year { get; set; }

        public DateTime? PublishedDate { get; set; }

        public string StatusText { get; set; } = "Yayında";

        public string CategoryText { get; set; } = "Bildiri Kitabı";
    }
}