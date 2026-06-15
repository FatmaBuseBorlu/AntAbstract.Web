using Microsoft.AspNetCore.Authorization;

namespace AntAbstract.Web.Security;

public sealed class TenantAdminAuthorizationHandler
    : AuthorizationHandler<TenantAdminRequirement>
{
    private readonly IAdminTenantAccessService _tenantAccess;

    public TenantAdminAuthorizationHandler(
        IAdminTenantAccessService tenantAccess)
    {
        _tenantAccess = tenantAccess;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantAdminRequirement requirement)
    {
        if (await _tenantAccess.CanAccessAdminAreaAsync(
                context.User,
                requirement.AllowSuperAdmin))
        {
            context.Succeed(requirement);
        }
    }
}
