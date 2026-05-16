using System;

namespace AntAbstract.WebUI.Models.ViewModels.Admin.AllConferences
{
    public class AllConferenceListItemViewModel
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Slug { get; set; }

        public string TenantName { get; set; } = "-";

        public string? TenantSlug { get; set; }

        public string? City { get; set; }

        public string? Country { get; set; }

        public string? Venue { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public int SubmissionCount { get; set; }

        public int RegistrationCount { get; set; }
    }
}