using System;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class AdminSubmissionsIndexModel
    {
        public string Slug { get; set; } = "";
        public Guid? ConferenceId { get; set; }
        public string? ConferenceTitle { get; set; }
        public string? Search { get; set; }
        public string? Status { get; set; }
        public List<AdminSubmissionRowModel> Items { get; set; } = new();
    }

    public class AdminSubmissionRowModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public string ConferenceTitle { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = "";
    }
}
