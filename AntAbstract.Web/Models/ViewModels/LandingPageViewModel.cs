using System;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class LandingPageViewModel
    {
        public int TotalUsers { get; set; }

        public int ActiveCongressesCount { get; set; }

        public List<CongressCardDto> ActiveCongresses { get; set; } = new();

        public List<SubmissionCardDto> LastSubmissions { get; set; } = new();
    }

    public class CongressCardDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public string Location { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public bool IsRegistered { get; set; }
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