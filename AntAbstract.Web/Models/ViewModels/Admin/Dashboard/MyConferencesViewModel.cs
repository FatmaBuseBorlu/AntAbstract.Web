using System;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels.Dashboard
{
    public class MyConferencesViewModel
    {
        public List<MyConferenceCardViewModel> RegisteredConferences { get; set; } = new();
        public List<MyConferenceCardViewModel> AvailableConferences { get; set; } = new();
    }

    public class MyConferenceCardViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? BannerPath { get; set; }
        public string? Slug { get; set; }
        public bool IsRegistered { get; set; }
    }
}