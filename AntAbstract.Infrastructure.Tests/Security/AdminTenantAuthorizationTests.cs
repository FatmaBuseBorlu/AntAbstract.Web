using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Security.Claims;
using Xunit;

namespace AntAbstract.Infrastructure.Tests.Security;

public class AdminTenantAuthorizationTests
{
    [Theory]
    [InlineData("AssignmentController", AdminPolicies.TenantAdmin)]
    [InlineData("AttendanceController", AdminPolicies.TenantAdmin)]
    [InlineData("BroadcastController", AdminPolicies.TenantAdmin)]
    [InlineData("ConferenceFlowController", AdminPolicies.TenantAdmin)]
    [InlineData("ConferencesController", AdminPolicies.TenantAdmin)]
    [InlineData("DecisionController", AdminPolicies.TenantAdmin)]
    [InlineData("PageBlocksController", AdminPolicies.TenantAdmin)]
    [InlineData("PaymentsController", AdminPolicies.TenantAdmin)]
    [InlineData("ProceedingBookController", AdminPolicies.TenantAdmin)]
    [InlineData("RefereeController", AdminPolicies.TenantAdmin)]
    [InlineData("ReviewCriteriaController", AdminPolicies.TenantAdmin)]
    [InlineData("SessionController", AdminPolicies.TenantAdmin)]
    [InlineData("SubmissionsController", AdminPolicies.TenantAdmin)]
    [InlineData("AuditLogsController", AdminPolicies.TenantAdmin)]
    [InlineData("HealthController", AdminPolicies.TenantAdmin)]
    [InlineData("CertificatesController", AdminPolicies.TenantAdminOnly)]
    [InlineData("ConferenceContextController", AdminPolicies.TenantAdminOnly)]
    [InlineData("ConferenceTopicsController", AdminPolicies.TenantAdminOnly)]
    [InlineData("RegistrationTypesController", AdminPolicies.TenantAdminOnly)]
    [InlineData("RegistrationsController", AdminPolicies.TenantAdminOnly)]
    [InlineData("ReportsController", AdminPolicies.TenantAdminOnly)]
    [InlineData("WebsiteController", AdminPolicies.TenantAdminOnly)]
    public void TenantAdminControllers_UseCentralPolicy(
        string controllerName,
        string expectedPolicy)
    {
        var controllerType = typeof(AdminPolicies).Assembly
            .GetTypes()
            .Single(type =>
                type.Name == controllerName &&
                type.Namespace?.Contains(
                    ".Areas.Admin.Controllers",
                    StringComparison.Ordinal) == true &&
                typeof(Controller).IsAssignableFrom(type));
        var authorizeAttributes = controllerType
            .GetCustomAttributes<AuthorizeAttribute>()
            .ToList();

        Assert.Contains(
            authorizeAttributes,
            attribute => attribute.Policy == expectedPolicy);
    }

    [Theory]
    [InlineData("AllConferencesController")]
    [InlineData("CentralVitrinController")]
    [InlineData("EmailTemplatesController")]
    [InlineData("SystemParametersController")]
    [InlineData("SystemReportsController")]
    [InlineData("TenantsController")]
    [InlineData("UsersController")]
    public void SuperAdminOnlyControllers_RequireSuperAdminRole(string controllerName)
    {
        var controllerType = typeof(AdminPolicies).Assembly
            .GetTypes()
            .Single(type =>
                type.Name == controllerName &&
                type.Namespace?.Contains(
                    ".Areas.Admin.Controllers",
                    StringComparison.Ordinal) == true &&
                typeof(Controller).IsAssignableFrom(type));

        var authorizeAttrs = controllerType
            .GetCustomAttributes<AuthorizeAttribute>()
            .ToList();

        Assert.Contains(
            authorizeAttrs,
            a => a.Roles != null && a.Roles.Split(',')
                .Select(r => r.Trim())
                .Contains("SuperAdmin"));
    }

    [Fact]
    public async Task Handler_AllowsAdminForMatchingCurrentTenant()
    {
        var tenant = CreateTenant("tenant-a");
        await using var context = CreateContext(
            new TenantContext { Current = tenant });
        await AddUserAsync(context, "admin-a", tenant.Id);
        var service = new AdminTenantAccessService(
            context,
            new TenantContext { Current = tenant });
        var handler = new TenantAdminAuthorizationHandler(service);
        var requirement = new TenantAdminRequirement(allowSuperAdmin: false);
        var authorizationContext = CreateAuthorizationContext(
            requirement,
            CreatePrincipal("admin-a", "Admin"));

        await handler.HandleAsync(authorizationContext);

        Assert.True(authorizationContext.HasSucceeded);
    }

    [Fact]
    public async Task Handler_RejectsAdminForAnotherCurrentTenant()
    {
        var tenantA = CreateTenant("tenant-a");
        var tenantB = CreateTenant("tenant-b");
        var tenantContext = new TenantContext { Current = tenantB };
        await using var context = CreateContext(tenantContext);
        await AddUserAsync(context, "admin-a", tenantA.Id);
        var service = new AdminTenantAccessService(context, tenantContext);
        var handler = new TenantAdminAuthorizationHandler(service);
        var requirement = new TenantAdminRequirement(allowSuperAdmin: false);
        var authorizationContext = CreateAuthorizationContext(
            requirement,
            CreatePrincipal("admin-a", "Admin"));

        await handler.HandleAsync(authorizationContext);

        Assert.False(authorizationContext.HasSucceeded);
    }

    [Fact]
    public async Task Handler_AllowsSuperAdminOnlyWhenRequirementPermitsIt()
    {
        var tenantContext = new TenantContext();
        await using var context = CreateContext(tenantContext);
        var service = new AdminTenantAccessService(context, tenantContext);
        var handler = new TenantAdminAuthorizationHandler(service);
        var principal = CreatePrincipal("super-admin", "SuperAdmin");
        var allowedRequirement = new TenantAdminRequirement(allowSuperAdmin: true);
        var deniedRequirement = new TenantAdminRequirement(allowSuperAdmin: false);
        var allowedContext = CreateAuthorizationContext(
            allowedRequirement,
            principal);
        var deniedContext = CreateAuthorizationContext(
            deniedRequirement,
            principal);

        await handler.HandleAsync(allowedContext);
        await handler.HandleAsync(deniedContext);

        Assert.True(allowedContext.HasSucceeded);
        Assert.False(deniedContext.HasSucceeded);
    }

    [Fact]
    public async Task AccessibleQueries_ScopeAdminAndKeepSuperAdminGlobal()
    {
        var tenantA = CreateTenant("tenant-a");
        var tenantB = CreateTenant("tenant-b");
        var conferenceA = CreateConference(tenantA.Id, "Conference A");
        var conferenceB = CreateConference(tenantB.Id, "Conference B");
        var tenantContext = new TenantContext();
        await using var context = CreateContext(tenantContext);

        context.Users.Add(new AppUser
        {
            Id = "admin-a",
            UserName = "admin-a",
            TenantId = tenantA.Id
        });
        context.Conferences.AddRange(conferenceA, conferenceB);
        context.Registrations.AddRange(
            CreateRegistration(conferenceA, "participant-a"),
            CreateRegistration(conferenceB, "participant-b"));
        await context.SaveChangesAsync();

        var service = new AdminTenantAccessService(context, tenantContext);
        var admin = CreatePrincipal("admin-a", "Admin");
        var superAdmin = CreatePrincipal("super-admin", "SuperAdmin");
        var adminConferences = await service.GetAccessibleConferenceQueryAsync(admin);
        var adminRegistrations = await service.GetAccessibleRegistrationQueryAsync(admin);
        var superAdminConferences = await service.GetAccessibleConferenceQueryAsync(superAdmin);

        Assert.Equal(
            ["Conference A"],
            await adminConferences.Select(x => x.Title).ToListAsync());
        Assert.Equal(
            ["participant-a"],
            await adminRegistrations.Select(x => x.AppUserId).ToListAsync());
        Assert.Equal(2, await superAdminConferences.CountAsync());
    }

    private static AppDbContext CreateContext(TenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin-tenant-authorization-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options, tenantContext);
    }

    private static async Task AddUserAsync(
        AppDbContext context,
        string userId,
        Guid? tenantId)
    {
        context.Users.Add(new AppUser
        {
            Id = userId,
            UserName = userId,
            TenantId = tenantId
        });
        await context.SaveChangesAsync();
    }

    private static AuthorizationHandlerContext CreateAuthorizationContext(
        TenantAdminRequirement requirement,
        ClaimsPrincipal principal)
    {
        return new AuthorizationHandlerContext(
            [requirement],
            principal,
            resource: null);
    }

    private static ClaimsPrincipal CreatePrincipal(
        string userId,
        params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            authenticationType: "Test",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role));
    }

    private static Tenant CreateTenant(string slug)
    {
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = slug,
            Slug = slug
        };
    }

    private static Conference CreateConference(Guid tenantId, string title)
    {
        return new Conference
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1)
        };
    }

    private static Registration CreateRegistration(
        Conference conference,
        string userId)
    {
        return new Registration
        {
            Id = Guid.NewGuid(),
            AppUserId = userId,
            ConferenceId = conference.Id,
            Conference = conference,
            RegistrationTypeId = Guid.NewGuid(),
            Amount = 100
        };
    }
}
