using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Http;

namespace AntAbstract.Infrastructure.Services
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

        private string Key
        {
            get
            {
                if (_tenantContext.Current == null) return "SelectedConferenceId";
                return $"SelectedConferenceId:{_tenantContext.Current.Id}";
            }
        }

        public Guid? GetSelectedConferenceId()
        {
            var session = _http.HttpContext?.Session;
            if (session == null)
            {
                return null;
            }

            var str = session.GetString(Key);
            if (Guid.TryParse(str, out var id))
            {
                return id;
            }

            var fallback = session.GetString("SelectedConferenceId");
            return Guid.TryParse(fallback, out var fallbackId) ? fallbackId : null;
        }

        public void SetSelectedConferenceId(Guid conferenceId)
        {
            _http.HttpContext?.Session.SetString(Key, conferenceId.ToString());
            var session = _http.HttpContext?.Session;
            if (session == null)
            {
                return;
            }

            session.SetString(Key, conferenceId.ToString());
            session.SetString("SelectedConferenceId", conferenceId.ToString());
        }

        public void Clear()
        {
            var session = _http.HttpContext?.Session;
            if (session == null) return;

            session.Remove(Key);
            session.Remove("SelectedConferenceId");
            session.Remove("SelectedConferenceSlug");
        }

    }
}
