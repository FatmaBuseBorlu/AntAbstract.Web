using System;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels.Admin.Submissions
{
    public class IncompleteSubmissionsViewModel
    {
        public string Slug { get; set; } = string.Empty;
        public Guid ConferenceId { get; set; }
        public string ConferenceTitle { get; set; } = string.Empty;

        public int TotalCount { get; set; }
        public int NoAbstractCount { get; set; }
        public int NoFileCount { get; set; }
        public int NoKeywordsCount { get; set; }
        public int StaleCount { get; set; }

        public List<IncompleteSubmissionRow> Items { get; set; } = new();

        public string? StatusFilter { get; set; }
        public string? MissingFilter { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int FilteredCount { get; set; }
        public int TotalPages => FilteredCount <= 0 ? 1 : (int)Math.Ceiling((double)FilteredCount / PageSize);
        public bool HasPrev => Page > 1;
        public bool HasNext => Page < TotalPages;
    }

    public class IncompleteSubmissionRow
    {
        public Guid Id { get; set; }
        public string SubmissionIdCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string AuthorEmail { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public bool HasAbstract { get; set; }
        public bool HasKeywords { get; set; }
        public bool HasFile { get; set; }
        public bool HasTopic { get; set; }
        public bool HasPresentationType { get; set; }
        public int DaysSinceCreation => (int)(DateTime.UtcNow - CreatedDate).TotalDays;
        public int DaysSinceActivity => UpdatedDate.HasValue
            ? (int)(DateTime.UtcNow - UpdatedDate.Value).TotalDays
            : DaysSinceCreation;
        public bool IsStale => DaysSinceActivity >= 7;
        public int MissingCount =>
            (HasAbstract ? 0 : 1) +
            (HasKeywords ? 0 : 1) +
            (HasFile ? 0 : 1) +
            (HasTopic ? 0 : 1) +
            (HasPresentationType ? 0 : 1);
    }
}
