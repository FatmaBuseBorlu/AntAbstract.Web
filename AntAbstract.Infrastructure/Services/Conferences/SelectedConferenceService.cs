using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Http;
using System;

namespace AntAbstract.Infrastructure.Services.Conferences
{
    public class SelectedConferenceService : ISelectedConferenceService
    {
        private const string GlobalConferenceIdKey = "SelectedConferenceId";
        private const string GlobalConferenceSlugKey = "SelectedConferenceSlug";
        private const string GlobalConferenceTitleKey = "SelectedConferenceTitle";

        private readonly IHttpContextAccessor _http;
        private readonly TenantContext _tenantContext;

        public SelectedConferenceService(
            IHttpContextAccessor http,
            TenantContext tenantContext)
        {
            _http = http;
            _tenantContext = tenantContext;
        }

        public Guid? GetSelectedConferenceId()
        {
            var session = _http.HttpContext?.Session;

            if (session == null)
            {
                return null;
            }

            string? conferenceIdText;

            if (_tenantContext.Current != null)
            {
                var tenantConferenceIdKey = BuildTenantConferenceIdKey(_tenantContext.Current.Id);

                conferenceIdText = session.GetString(tenantConferenceIdKey);

                return Guid.TryParse(conferenceIdText, out var tenantConferenceId)
                    ? tenantConferenceId
                    : null;
            }

            conferenceIdText = session.GetString(GlobalConferenceIdKey);

            return Guid.TryParse(conferenceIdText, out var globalConferenceId)
                ? globalConferenceId
                : null;
        }

        public void SetSelectedConferenceId(Guid conferenceId)
        {
            var session = _http.HttpContext?.Session;

            if (session == null || conferenceId == Guid.Empty)
            {
                return;
            }

            session.SetString(GlobalConferenceIdKey, conferenceId.ToString());

            if (_tenantContext.Current != null)
            {
                var tenantConferenceIdKey = BuildTenantConferenceIdKey(_tenantContext.Current.Id);

                session.SetString(tenantConferenceIdKey, conferenceId.ToString());
            }
        }

        public void ClearSelectedConferenceId()
        {
            var session = _http.HttpContext?.Session;

            if (session == null)
            {
                return;
            }

            session.Remove(GlobalConferenceIdKey);
            session.Remove(GlobalConferenceSlugKey);
            session.Remove(GlobalConferenceTitleKey);

            if (_tenantContext.Current != null)
            {
                var tenantId = _tenantContext.Current.Id;

                session.Remove(BuildTenantConferenceIdKey(tenantId));
                session.Remove(BuildTenantConferenceSlugKey(tenantId));
                session.Remove(BuildTenantConferenceTitleKey(tenantId));
            }
        }

        private static string BuildTenantConferenceIdKey(Guid tenantId)
        {
            return $"SelectedConferenceId:{tenantId}";
        }

        private static string BuildTenantConferenceSlugKey(Guid tenantId)
        {
            return $"SelectedConferenceSlug:{tenantId}";
        }

        private static string BuildTenantConferenceTitleKey(Guid tenantId)
        {
            return $"SelectedConferenceTitle:{tenantId}";
        }
    }
}