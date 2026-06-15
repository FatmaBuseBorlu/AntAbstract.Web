using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AntAbstract.Web.Security;

public sealed class AdminTenantAccessService : IAdminTenantAccessService
{
    private readonly AppDbContext _context;
    private readonly TenantContext _tenantContext;
    private string? _cachedUserId;
    private Guid? _cachedTenantId;
    private bool _tenantLoaded;

    public AdminTenantAccessService(
        AppDbContext context,
        TenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public bool IsSuperAdmin(ClaimsPrincipal principal)
    {
        return principal.IsInRole("SuperAdmin");
    }

    public async Task<Guid?> GetAdminTenantIdAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        if (_tenantLoaded &&
            string.Equals(_cachedUserId, userId, StringComparison.Ordinal))
        {
            return _cachedTenantId;
        }

        _cachedUserId = userId;
        _cachedTenantId = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        _tenantLoaded = true;

        return _cachedTenantId;
    }

    public async Task<bool> CanAccessAdminAreaAsync(
        ClaimsPrincipal principal,
        bool allowSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        if (allowSuperAdmin && IsSuperAdmin(principal))
        {
            return true;
        }

        if (!principal.IsInRole("Admin"))
        {
            return false;
        }

        var tenantId = await GetAdminTenantIdAsync(principal, cancellationToken);

        if (!tenantId.HasValue)
        {
            return false;
        }

        return !_tenantContext.CurrentTenantId.HasValue ||
               _tenantContext.CurrentTenantId == tenantId;
    }

    public async Task<bool> CanAccessCurrentTenantAsync(
        ClaimsPrincipal principal,
        string? slug = null,
        bool allowSuperAdmin = true,
        CancellationToken cancellationToken = default)
    {
        var currentTenant = _tenantContext.Current;

        if (currentTenant == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(slug) &&
            !string.Equals(
                currentTenant.Slug,
                slug,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (allowSuperAdmin && IsSuperAdmin(principal))
        {
            return true;
        }

        if (!principal.IsInRole("Admin"))
        {
            return false;
        }

        var tenantId = await GetAdminTenantIdAsync(principal, cancellationToken);
        return tenantId.HasValue && tenantId.Value == currentTenant.Id;
    }

    public async Task<IQueryable<Conference>> GetAccessibleConferenceQueryAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Conferences.AsQueryable();

        if (IsSuperAdmin(principal))
        {
            return query;
        }

        if (!principal.IsInRole("Admin"))
        {
            return query.Where(_ => false);
        }

        var tenantId = await GetAdminTenantIdAsync(principal, cancellationToken);

        return tenantId.HasValue
            ? query.Where(conference => conference.TenantId == tenantId.Value)
            : query.Where(_ => false);
    }

    public async Task<IQueryable<Registration>> GetAccessibleRegistrationQueryAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Registrations.AsQueryable();

        if (IsSuperAdmin(principal))
        {
            return query;
        }

        if (!principal.IsInRole("Admin"))
        {
            return query.Where(_ => false);
        }

        var tenantId = await GetAdminTenantIdAsync(principal, cancellationToken);

        return tenantId.HasValue
            ? query.Where(registration =>
                registration.Conference != null &&
                registration.Conference.TenantId == tenantId.Value)
            : query.Where(_ => false);
    }
}
