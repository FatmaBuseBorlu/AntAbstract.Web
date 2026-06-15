using AntAbstract.Domain.Entities;
using System.Security.Claims;

namespace AntAbstract.Web.Security;

public interface IAdminTenantAccessService
{
    bool IsSuperAdmin(ClaimsPrincipal principal);

    Task<Guid?> GetAdminTenantIdAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessAdminAreaAsync(
        ClaimsPrincipal principal,
        bool allowSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessCurrentTenantAsync(
        ClaimsPrincipal principal,
        string? slug = null,
        bool allowSuperAdmin = true,
        CancellationToken cancellationToken = default);

    Task<IQueryable<Conference>> GetAccessibleConferenceQueryAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<IQueryable<Registration>> GetAccessibleRegistrationQueryAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
