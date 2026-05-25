using System;
using System.Collections.Generic;
using System.Linq;

namespace AntAbstract.Web.Models.ViewModels.Admin.Tenants
{
    public class TenantListItemViewModel
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Slug { get; set; }

        public string? ScientificFieldName { get; set; }

        public string? CongressTypeName { get; set; }

        public int ConferenceCount { get; set; }

        public int UserCount { get; set; }

        public List<string> AdminNames { get; set; } = new();

        public int AdminCount => AdminNames?.Count ?? 0;

        public bool HasAdmin => AdminCount > 0;

        public string SlugPath => string.IsNullOrWhiteSpace(Slug)
            ? "-"
            : $"/{Slug}";

        public string AdminDisplayText
        {
            get
            {
                if (AdminNames == null || !AdminNames.Any())
                {
                    return "Admin atanmamış";
                }

                var visibleAdmins = AdminNames.Take(2).ToList();

                if (AdminNames.Count <= 2)
                {
                    return string.Join(", ", visibleAdmins);
                }

                return $"{string.Join(", ", visibleAdmins)} +{AdminNames.Count - 2}";
            }
        }
    }
}