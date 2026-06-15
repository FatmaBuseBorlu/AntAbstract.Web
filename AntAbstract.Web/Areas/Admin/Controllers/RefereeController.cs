using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.Referee;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class RefereeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly IStringLocalizer<RefereeController> _localizer;

        private static readonly string[] ReviewerRoleNames =
        {
            "Referee",
            "Hakem",
            "Reviewer"
        };

        private const string PrimaryReviewerRoleName = "Referee";

        public RefereeController(
            AppDbContext context,
            TenantContext tenantContext,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ISelectedConferenceService selectedConferenceService,
            IStringLocalizer<RefereeController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _userManager = userManager;
            _roleManager = roleManager;
            _selectedConferenceService = selectedConferenceService;
            _localizer = localizer;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private bool IsSuperAdminUser()
        {
            return User.IsInRole("SuperAdmin");
        }

        private async Task<AppUser?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        private async Task<Guid?> GetCurrentAdminTenantIdAsync()
        {
            var currentUser = await GetCurrentUserAsync();

            return currentUser?.TenantId;
        }

        private async Task EnsurePrimaryReviewerRoleExistsAsync()
        {
            if (!await _roleManager.RoleExistsAsync(PrimaryReviewerRoleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(PrimaryReviewerRoleName));
            }
        }

        private async Task<List<AppUser>> GetUsersInReviewerRolesAsync()
        {
            var reviewerUsers = new Dictionary<string, AppUser>(StringComparer.OrdinalIgnoreCase);

            foreach (var roleName in ReviewerRoleNames)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    continue;
                }

                var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);

                foreach (var user in usersInRole)
                {
                    if (user == null || string.IsNullOrWhiteSpace(user.Id))
                    {
                        continue;
                    }

                    reviewerUsers[user.Id] = user;
                }
            }

            return reviewerUsers.Values.ToList();
        }

        private async Task<bool> UserHasAnyReviewerRoleAsync(AppUser user)
        {
            foreach (var roleName in ReviewerRoleNames)
            {
                if (await _roleManager.RoleExistsAsync(roleName) &&
                    await _userManager.IsInRoleAsync(user, roleName))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<Conference?> GetAccessibleConferenceAsync(
            string? slug,
            Guid? conferenceId)
        {
            Guid? selectedConferenceId = null;

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                selectedConferenceId = conferenceId.Value;
            }
            else
            {
                selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();
            }

            if (!selectedConferenceId.HasValue || selectedConferenceId.Value == Guid.Empty)
            {
                return null;
            }

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (IsSuperAdminUser())
            {
                if (!string.IsNullOrWhiteSpace(slug))
                {
                    return await query.FirstOrDefaultAsync(c =>
                        c.Id == selectedConferenceId.Value &&
                        c.Tenant != null &&
                        c.Tenant.Slug == slug);
                }

                return await query.FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value);
            }

            var adminTenantId = await GetCurrentAdminTenantIdAsync();

            if (!adminTenantId.HasValue)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(slug))
            {
                return await query.FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value &&
                    c.TenantId == adminTenantId.Value &&
                    c.Tenant != null &&
                    c.Tenant.Slug == slug);
            }

            return await query.FirstOrDefaultAsync(c =>
                c.Id == selectedConferenceId.Value &&
                c.TenantId == adminTenantId.Value);
        }

        private void SetSelectedConferenceSession(Conference conference)
        {
            var slug = conference.Tenant?.Slug ?? _tenantContext.Current?.Slug ?? "";

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{conference.TenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{conference.TenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{conference.TenantId}", conference.Title ?? "");
        }

        private async Task<Guid?> GetTargetTenantIdAsync(
            string? slug,
            Guid? conferenceId)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference != null)
            {
                SetSelectedConferenceSession(conference);

                ViewBag.ConferenceId = conference.Id;
                ViewBag.ConferenceTitle = conference.Title ?? "";
                ViewBag.Slug = conference.Tenant?.Slug ?? slug ?? "";
                ViewBag.TargetTenantId = conference.TenantId;

                return conference.TenantId;
            }

            if (IsSuperAdminUser())
            {
                ViewBag.ConferenceId = null;
                ViewBag.ConferenceTitle = "";
                ViewBag.Slug = slug ?? "";
                ViewBag.TargetTenantId = null;

                return null;
            }

            var adminTenantId = await GetCurrentAdminTenantIdAsync();

            ViewBag.ConferenceId = null;
            ViewBag.ConferenceTitle = "";
            ViewBag.Slug = slug ?? "";
            ViewBag.TargetTenantId = adminTenantId;

            return adminTenantId;
        }

        private string BuildRefereeIndexUrl(string? slug, Guid? conferenceId)
        {
            if (!string.IsNullOrWhiteSpace(slug) &&
                conferenceId.HasValue &&
                conferenceId.Value != Guid.Empty)
            {
                return $"/{slug}/Admin/Referee?conferenceId={conferenceId.Value}";
            }

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                return $"/Admin/Referee?conferenceId={conferenceId.Value}";
            }

            return "/Admin/Referee";
        }

        private string BuildRefereeCreateUrl(string? slug, Guid? conferenceId)
        {
            if (!string.IsNullOrWhiteSpace(slug) &&
                conferenceId.HasValue &&
                conferenceId.Value != Guid.Empty)
            {
                return $"/{slug}/Admin/Referee/Create?conferenceId={conferenceId.Value}";
            }

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                return $"/Admin/Referee/Create?conferenceId={conferenceId.Value}";
            }

            return "/Admin/Referee/Create";
        }

        private async Task<List<AppUser>> GetRefereeListAsync(Guid? targetTenantId)
        {
            var referees = await GetUsersInReviewerRolesAsync();

            if (targetTenantId.HasValue)
            {
                return referees
                    .Where(r =>
                        r.TenantId.HasValue &&
                        r.TenantId.Value == targetTenantId.Value)
                    .OrderBy(r => r.FirstName)
                    .ThenBy(r => r.LastName)
                    .ThenBy(r => r.Email)
                    .ToList();
            }

            if (IsSuperAdminUser())
            {
                return referees
                    .OrderBy(r => r.FirstName)
                    .ThenBy(r => r.LastName)
                    .ThenBy(r => r.Email)
                    .ToList();
            }

            return new List<AppUser>();
        }

        [HttpGet("/Admin/Referee")]
        public async Task<IActionResult> Index(Guid? conferenceId = null)
        {
            var conference = await GetAccessibleConferenceAsync(null, conferenceId);

            if (conference != null &&
                conference.Tenant != null &&
                !string.IsNullOrWhiteSpace(conference.Tenant.Slug))
            {
                SetSelectedConferenceSession(conference);

                return Redirect(BuildRefereeIndexUrl(
                    conference.Tenant.Slug,
                    conference.Id));
            }

            var targetTenantId = await GetTargetTenantIdAsync(null, conferenceId);

            if (!targetTenantId.HasValue && !IsSuperAdminUser())
            {
                TempData["ErrorMessage"] = T(
                    "Error_AdminTenantNotFound",
                    "Admin hesabınıza bağlı kurum bulunamadı.");

                return View(new List<AppUser>());
            }

            if (!targetTenantId.HasValue && IsSuperAdminUser())
            {
                ViewBag.ConferenceId = null;
                ViewBag.ConferenceTitle = "";
                ViewBag.Slug = "";
                ViewBag.TargetTenantId = null;
            }

            var filteredReferees = await GetRefereeListAsync(targetTenantId);

            return View(filteredReferees);
        }

        [HttpGet("/{slug}/Admin/Referee")]
        public async Task<IActionResult> TenantIndex(
            string slug,
            Guid? conferenceId = null)
        {
            var targetTenantId = await GetTargetTenantIdAsync(slug, conferenceId);

            if (!targetTenantId.HasValue)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectValidConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return Redirect("/Admin/ConferenceFlow");
            }

            var filteredReferees = await GetRefereeListAsync(targetTenantId);

            return View("Index", filteredReferees);
        }

        [HttpGet("/Admin/Referee/Create")]
        public async Task<IActionResult> Create(Guid? conferenceId = null)
        {
            var conference = await GetAccessibleConferenceAsync(null, conferenceId);

            if (conference != null &&
                conference.Tenant != null &&
                !string.IsNullOrWhiteSpace(conference.Tenant.Slug))
            {
                SetSelectedConferenceSession(conference);

                return Redirect(BuildRefereeCreateUrl(
                    conference.Tenant.Slug,
                    conference.Id));
            }

            var targetTenantId = await GetTargetTenantIdAsync(null, conferenceId);

            if (!targetTenantId.HasValue)
            {
                TempData["ErrorMessage"] = IsSuperAdminUser()
                    ? T("Error_SelectConferenceForRefereeCreate", "Hakem oluşturmak için önce bir kongre seçiniz.")
                    : T("Error_AdminTenantNotFound", "Hakem oluşturmak için admin hesabınıza bağlı bir kurum bulunmalıdır.");

                return RedirectToAction(nameof(Index));
            }

            ViewBag.TargetTenantId = targetTenantId.Value;

            return View();
        }

        [HttpGet("/{slug}/Admin/Referee/Create")]
        public async Task<IActionResult> CreateForConference(
            string slug,
            Guid? conferenceId = null)
        {
            var targetTenantId = await GetTargetTenantIdAsync(slug, conferenceId);

            if (!targetTenantId.HasValue)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectValidConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return Redirect("/Admin/ConferenceFlow");
            }

            ViewBag.TargetTenantId = targetTenantId.Value;

            return View("Create");
        }

        [HttpPost("/Admin/Referee/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            RefereeCreateViewModel model,
            Guid? conferenceId = null)
        {
            var conference = await GetAccessibleConferenceAsync(null, conferenceId);

            if (conference != null &&
                conference.Tenant != null &&
                !string.IsNullOrWhiteSpace(conference.Tenant.Slug))
            {
                return await CreateForConferencePost(
                    conference.Tenant.Slug,
                    model,
                    conference.Id);
            }

            var targetTenantId = await GetTargetTenantIdAsync(null, conferenceId);

            if (!targetTenantId.HasValue)
            {
                TempData["ErrorMessage"] = IsSuperAdminUser()
                    ? T("Error_SelectConferenceForRefereeCreate", "Hakem oluşturmak için önce bir kongre seçiniz.")
                    : T("Error_AdminTenantNotFound", "Hakem oluşturmak için admin hesabınıza bağlı bir kurum bulunmalıdır.");

                return RedirectToAction(nameof(Index));
            }

            return await CreateRefereeAsync(
                model,
                targetTenantId.Value,
                null,
                conferenceId);
        }

        [HttpPost("/{slug}/Admin/Referee/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateForConferencePost(
            string slug,
            RefereeCreateViewModel model,
            Guid? conferenceId = null)
        {
            var targetTenantId = await GetTargetTenantIdAsync(slug, conferenceId);

            if (!targetTenantId.HasValue)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectValidConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return Redirect("/Admin/ConferenceFlow");
            }

            return await CreateRefereeAsync(
                model,
                targetTenantId.Value,
                slug,
                conferenceId);
        }

        private async Task<IActionResult> CreateRefereeAsync(
            RefereeCreateViewModel model,
            Guid targetTenantId,
            string? slug,
            Guid? conferenceId)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.TargetTenantId = targetTenantId;

                return View("Create", model);
            }

            await EnsurePrimaryReviewerRoleExistsAsync();

            var existingUser = await _userManager.FindByEmailAsync(model.Email);

            if (existingUser != null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    T("Error_EmailAlreadyExists", "Bu e-posta adresiyle kayıtlı bir kullanıcı zaten var."));

                ViewBag.TargetTenantId = targetTenantId;

                return View("Create", model);
            }

            var refereeUser = new AppUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Institution = model.Institution,
                TenantId = targetTenantId,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(
                refereeUser,
                model.Password);

            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                ViewBag.TargetTenantId = targetTenantId;

                return View("Create", model);
            }

            var roleResult = await _userManager.AddToRoleAsync(
                refereeUser,
                PrimaryReviewerRoleName);

            if (!roleResult.Succeeded)
            {
                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                ViewBag.TargetTenantId = targetTenantId;

                return View("Create", model);
            }

            TempData["SuccessMessage"] = T(
                "Success_RefereeCreated",
                "Hakem başarıyla oluşturuldu.");

            return Redirect(BuildRefereeIndexUrl(slug, conferenceId));
        }

        [HttpPost("/Admin/Referee/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(
            string id,
            Guid? conferenceId = null)
        {
            var conference = await GetAccessibleConferenceAsync(null, conferenceId);

            if (conference != null &&
                conference.Tenant != null &&
                !string.IsNullOrWhiteSpace(conference.Tenant.Slug))
            {
                return await DeleteForConference(
                    conference.Tenant.Slug,
                    id,
                    conference.Id);
            }

            var targetTenantId = await GetTargetTenantIdAsync(null, conferenceId);

            return await DeleteRefereeAsync(
                id,
                targetTenantId,
                null,
                conferenceId);
        }

        [HttpPost("/{slug}/Admin/Referee/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteForConference(
            string slug,
            string id,
            Guid? conferenceId = null)
        {
            var targetTenantId = await GetTargetTenantIdAsync(slug, conferenceId);

            return await DeleteRefereeAsync(
                id,
                targetTenantId,
                slug,
                conferenceId);
        }

        private async Task<IActionResult> DeleteRefereeAsync(
            string id,
            Guid? targetTenantId,
            string? slug,
            Guid? conferenceId)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidUser",
                    "Geçersiz kullanıcı.");

                return Redirect(BuildRefereeIndexUrl(slug, conferenceId));
            }

            var currentUser = await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return Challenge();
            }

            var refereeUser = await _userManager.FindByIdAsync(id);

            if (refereeUser == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UserNotFound",
                    "Kullanıcı bulunamadı.");

                return Redirect(BuildRefereeIndexUrl(slug, conferenceId));
            }

            if (refereeUser.Id == currentUser.Id)
            {
                TempData["ErrorMessage"] = T(
                    "Error_CannotRemoveOwnRefereeRole",
                    "Kendi hesabınızın hakem rolünü bu ekrandan kaldıramazsınız.");

                return Redirect(BuildRefereeIndexUrl(slug, conferenceId));
            }

            if (!IsSuperAdminUser())
            {
                if (!targetTenantId.HasValue)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_AdminTenantNotFound",
                        "Admin hesabınıza bağlı kurum bulunamadı.");

                    return Redirect(BuildRefereeIndexUrl(slug, conferenceId));
                }

                if (!refereeUser.TenantId.HasValue ||
                    refereeUser.TenantId.Value != targetTenantId.Value)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_UnauthorizedDelete",
                        "Bu kullanıcının hakem rolünü kaldırma yetkiniz yok.");

                    return Redirect(BuildRefereeIndexUrl(slug, conferenceId));
                }
            }
            else if (targetTenantId.HasValue)
            {
                if (!refereeUser.TenantId.HasValue ||
                    refereeUser.TenantId.Value != targetTenantId.Value)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_UnauthorizedDelete",
                        "Bu kullanıcı seçili kongrenin kurumuna bağlı değil.");

                    return Redirect(BuildRefereeIndexUrl(slug, conferenceId));
                }
            }

            var hasReviewerRole = await UserHasAnyReviewerRoleAsync(refereeUser);

            if (!hasReviewerRole)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UserIsNotReferee",
                    "Bu kullanıcı zaten hakem rolüne sahip değil.");

                return Redirect(BuildRefereeIndexUrl(slug, conferenceId));
            }

            var hasPendingReviewAssignmentQuery = _context.ReviewAssignments
                .AsNoTracking()
                .Include(x => x.Submission)
                .Where(x =>
                    x.ReviewerId == refereeUser.Id &&
                    x.Review == null);

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                hasPendingReviewAssignmentQuery = hasPendingReviewAssignmentQuery
                    .Where(x =>
                        x.Submission != null &&
                        x.Submission.ConferenceId == conferenceId.Value);
            }

            var hasPendingReviewAssignment = await hasPendingReviewAssignmentQuery.AnyAsync();

            if (hasPendingReviewAssignment)
            {
                TempData["ErrorMessage"] = T(
                    "Error_RefereeHasPendingReviews",
                    "Bu hakemin tamamlanmamış değerlendirmesi olduğu için hakem rolü kaldırılamaz.");

                return Redirect(BuildRefereeIndexUrl(slug, conferenceId));
            }

            var rolesRemoved = new List<string>();

            foreach (var roleName in ReviewerRoleNames)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    continue;
                }

                if (!await _userManager.IsInRoleAsync(refereeUser, roleName))
                {
                    continue;
                }

                var removeRoleResult = await _userManager.RemoveFromRoleAsync(
                    refereeUser,
                    roleName);

                if (!removeRoleResult.Succeeded)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_RefereeRoleRemoveFailed",
                        "Hakem rolü kaldırılırken bir hata oluştu.");

                    return Redirect(BuildRefereeIndexUrl(slug, conferenceId));
                }

                rolesRemoved.Add(roleName);
            }

            if (!rolesRemoved.Any())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UserIsNotReferee",
                    "Bu kullanıcı zaten hakem rolüne sahip değil.");

                return Redirect(BuildRefereeIndexUrl(slug, conferenceId));
            }

            var remainingRoles = await _userManager.GetRolesAsync(refereeUser);

            if (!remainingRoles.Any())
            {
                refereeUser.TenantId = null;

                var updateUserResult = await _userManager.UpdateAsync(refereeUser);

                if (!updateUserResult.Succeeded)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_RefereeRoleRemovedButTenantClearFailed",
                        "Hakem rolü kaldırıldı fakat kullanıcının kurum bağlantısı temizlenirken bir hata oluştu.");

                    return Redirect(BuildRefereeIndexUrl(slug, conferenceId));
                }

                TempData["SuccessMessage"] = T(
                    "Success_RefereeRoleRemovedAndTenantCleared",
                    "Hakem rolü kaldırıldı ve kullanıcının kurum bağlantısı temizlendi.");

                return Redirect(BuildRefereeIndexUrl(slug, conferenceId));
            }

            TempData["SuccessMessage"] = T(
                "Success_RefereeRoleRemoved",
                "Hakem rolü başarıyla kaldırıldı. Kullanıcının diğer rolleri korundu.");

            return Redirect(BuildRefereeIndexUrl(slug, conferenceId));
        }
    }
}