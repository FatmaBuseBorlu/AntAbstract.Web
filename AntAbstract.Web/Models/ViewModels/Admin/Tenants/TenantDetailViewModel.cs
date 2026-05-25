using System;
using System.Collections.Generic;
using System.Linq;

namespace AntAbstract.Web.Models.ViewModels.Admin.Tenants
{
    public class TenantDetailViewModel
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Slug { get; set; }

        public string? LogoUrl { get; set; }

        public string? ScientificFieldName { get; set; }

        public string? CongressTypeName { get; set; }

        public string? ConferenceFlowUrl { get; set; }

        public string? AssignManagerReturnUrl { get; set; }

        public List<TenantDetailUserViewModel> Admins { get; set; } = new();

        public List<TenantDetailUserViewModel> Users { get; set; } = new();

        public List<TenantDetailConferenceViewModel> Conferences { get; set; } = new();

        public int AdminCount => Admins?.Count ?? 0;

        public int UserCount => Users?.Count ?? 0;

        public int ConferenceCount => Conferences?.Count ?? 0;

        public bool HasAdmin => AdminCount > 0;

        public bool HasConference => ConferenceCount > 0;

        public string SlugPath => string.IsNullOrWhiteSpace(Slug)
            ? "-"
            : $"/{Slug}";

        public string PublicSiteUrl => string.IsNullOrWhiteSpace(Slug)
            ? "#"
            : $"/{Slug}";

        public string AdminDisplayText
        {
            get
            {
                if (Admins == null || !Admins.Any())
                {
                    return "Admin atanmamış";
                }

                var visibleAdmins = Admins
                    .Take(2)
                    .Select(x => x.DisplayName)
                    .ToList();

                if (Admins.Count <= 2)
                {
                    return string.Join(", ", visibleAdmins);
                }

                return $"{string.Join(", ", visibleAdmins)} +{Admins.Count - 2}";
            }
        }
    }

    public class TenantDetailUserViewModel
    {
        public string UserId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string? Email { get; set; }

        public List<string> Roles { get; set; } = new();

        public string RoleDisplayText => Roles != null && Roles.Any()
            ? string.Join(", ", Roles)
            : "Rol yok";
    }

    public class TenantDetailConferenceViewModel
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Slug { get; set; }

        public DateTime? StartDate { get; set; }

        public string StatusText { get; set; } = "Aktif";

        public string SlugPath => string.IsNullOrWhiteSpace(Slug)
            ? "-"
            : $"/{Slug}";
    }
}