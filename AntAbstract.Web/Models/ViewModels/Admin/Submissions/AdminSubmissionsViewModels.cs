using System;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels.Admin.Submissions
{
    public class AdminSubmissionsIndexModel
    {
        public string Slug { get; set; } = "";
        public Guid? ConferenceId { get; set; }
        public string? ConferenceTitle { get; set; }
        public string? Search { get; set; }
        public string? Status { get; set; }
        public List<AdminSubmissionRowModel> Items { get; set; } = new();

        // Pagination
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPrev => Page > 1;
        public bool HasNext => Page < TotalPages;
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
