using System;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class LandingPageViewModel
    {
        public int TotalUsers { get; set; }

        public int ActiveCongressesCount { get; set; }

        public List<CongressCardDto> ActiveCongresses { get; set; } = new();

        public List<CongressCardDto> PastCongresses { get; set; } = new();

        public List<ProceedingBookCardDto> ProceedingBooks { get; set; } = new();

        public List<SubmissionCardDto> LastSubmissions { get; set; } = new();
    }

    public class ProceedingBookCardDto
    {
        public Guid ConferenceId { get; set; }
        public string ConferenceTitle { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public DateTime? PublishedDate { get; set; }
        public string? FilePath { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Location { get; set; } = string.Empty;
    }

    public class CongressCardDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public string Location { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public bool IsRegistered { get; set; }

        public bool IsSubmissionOpen { get; set; }

        public bool IsRegistrationOpen { get; set; }

        public DateTime? AbstractSubmissionDeadline { get; set; }
    }

    public class SubmissionCardDto
    {
        public string Title { get; set; } = string.Empty;

        public string AbstractSnippet { get; set; } = string.Empty;

        public string AuthorName { get; set; } = string.Empty;

        public string University { get; set; } = string.Empty;

        public string ConferenceName { get; set; } = string.Empty;

        public string AuthorImageUrl { get; set; } = string.Empty;
    }
}