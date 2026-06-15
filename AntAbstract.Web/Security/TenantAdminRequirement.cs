using Microsoft.AspNetCore.Authorization;

namespace AntAbstract.Web.Security;

public sealed class TenantAdminRequirement : IAuthorizationRequirement
{
    public TenantAdminRequirement(bool allowSuperAdmin)
    {
        AllowSuperAdmin = allowSuperAdmin;
    }

    public bool AllowSuperAdmin { get; }
}
