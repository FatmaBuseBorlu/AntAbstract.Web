using System;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels.Admin.Assignment
{
    public class AssignmentIndexViewModel
    {
        public string Slug { get; set; } = "";
        public Guid ConferenceId { get; set; }
        public string ConferenceTitle { get; set; } = "";

        // Filters
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? AssignedFilter { get; set; } // "unassigned" | "assigned" | ""

        // Conference-wide stats (unfiltered)
        public int TotalCount { get; set; }
        public int AssignedCount { get; set; }
        public int UnassignedCount => TotalCount - AssignedCount;
        public int UnderReviewCount { get; set; }

        // Paged items
        public List<AssignmentRowModel> Items { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 30;
        public int FilteredCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)FilteredCount / PageSize);
        public bool HasPrev => Page > 1;
        public bool HasNext => Page < TotalPages;
    }

    public class AssignmentRowModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public string Status { get; set; } = "";
        public string Topic { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public int ReviewerCount { get; set; }
    }
}
