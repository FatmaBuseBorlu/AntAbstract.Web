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
            var str = _http.HttpContext?.Session.GetString(Key);
            return Guid.TryParse(str, out var id) ? id : null;
        }

        public void SetSelectedConferenceId(Guid conferenceId)
        {
            _http.HttpContext?.Session.SetString(Key, conferenceId.ToString());
        }

        public void Clear()
        {
            _http.HttpContext?.Session.Remove(Key);
        }
    }
}
