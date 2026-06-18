using System;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels.Admin.Conferences
{
    public class ConferencesIndexViewModel
    {
        public string Slug { get; set; } = string.Empty;
        public bool IsSuperAdmin { get; set; }

        public string? Search { get; set; }
        public string? StatusFilter { get; set; }

        public int TotalCount { get; set; }
        public int UpcomingCount { get; set; }
        public int OngoingCount { get; set; }
        public int PastCount { get; set; }

        public List<ConferenceRowModel> Items { get; set; } = new();

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;
        public int FilteredCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)FilteredCount / PageSize);
        public bool HasPrev => Page > 1;
        public bool HasNext => Page < TotalPages;
    }

    public class ConferenceRowModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? City { get; set; }
        public string? Venue { get; set; }
        public string? Country { get; set; }
        public string? LogoPath { get; set; }
        public string? BannerPath { get; set; }
        public bool IsSubmissionOpen { get; set; }
        public bool IsRegistrationOpen { get; set; }
        public int? MaxRegistrations { get; set; }
        public string? TenantName { get; set; }

        public string Status
        {
            get
            {
                var now = DateTime.Now;
                if (StartDate > now) return "upcoming";
                if (EndDate >= now) return "ongoing";
                return "past";
            }
        }
    }
}
