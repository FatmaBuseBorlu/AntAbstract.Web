using System;
using System.Collections.Generic;

namespace AntAbstract.Infrastructure.Services.Doi
{
    public sealed class DoiMetadataPreview
    {
        public bool IsConfigured { get; set; }

        public string Provider { get; set; } = "Manual";

        public string? Prefix { get; set; }

        public string? SuggestedDoi { get; set; }

        public string? SuggestedDoiUrl { get; set; }

        public string? LandingUrl { get; set; }

        public string Title { get; set; } = "";

        public string ConferenceTitle { get; set; } = "";

        public DateTime? PublicationDate { get; set; }

        public List<string> Authors { get; set; } = new();

        public List<string> MissingSettings { get; set; } = new();
    }
}
