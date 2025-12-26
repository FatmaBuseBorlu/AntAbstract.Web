using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using System;

namespace AntAbstract.Web.Models.ViewModels
{
    public class SelectedConferenceService : ISelectedConferenceService
    {
        private readonly IHttpContextAccessor _http;
        private readonly TenantContext _tenantContext;

        public SelectedConferenceService(IHttpContextAccessor http, TenantContext tenantContext)
        {
            _http = http;
            _tenantContext = tenantContext;
        }

        public Guid? GetSelectedConferenceId()
        {
            var session = _http.HttpContext?.Session;
            if (session == null)
                return null;

            string? confIdStr = null;

            if (_tenantContext.Current != null)
            {
                var tenantKey = $"SelectedConferenceId:{_tenantContext.Current.Id}";
                confIdStr = session.GetString(tenantKey);
            }

            confIdStr ??= session.GetString("SelectedConferenceId");

            return Guid.TryParse(confIdStr, out var id) ? id : null;
        }

        public void SetSelectedConferenceId(Guid conferenceId)
        {
            var session = _http.HttpContext?.Session;
            if (session == null)
                return;

            session.SetString("SelectedConferenceId", conferenceId.ToString());

            if (_tenantContext.Current != null)
            {
                var tenantKey = $"SelectedConferenceId:{_tenantContext.Current.Id}";
                session.SetString(tenantKey, conferenceId.ToString());
            }
        }

        public void ClearSelectedConferenceId()
        {
            var session = _http.HttpContext?.Session;
            if (session == null)
                return;

            session.Remove("SelectedConferenceId");

            if (_tenantContext.Current != null)
            {
                var tenantKey = $"SelectedConferenceId:{_tenantContext.Current.Id}";
                session.Remove(tenantKey);
            }
        }
    }
}
