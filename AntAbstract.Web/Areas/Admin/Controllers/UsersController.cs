using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Web.Models.ViewModels.Admin.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AntAbstract.Infrastructure.Context;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin")]
    public class UsersController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IStringLocalizer<UsersController> _localizer;
        private readonly AppDbContext _context;
        private readonly IAuditService _audit;
        private readonly ILogger<UsersController> _logger;

        private static readonly string[] AllowedRoles =
        {
            "SuperAdmin",
            "Admin",
            "Author",
            "Listener",
            "Referee"
        };

        public UsersController(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IStringLocalizer<UsersController> localizer,
            AppDbContext context,
            IAuditService audit,
            ILogger<UsersController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _localizer = localizer;
            _context = context;
            _audit = audit;
            _logger = logger;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private static bool IsAllowedRole(string roleName)
        {
            return AllowedRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
        }

        private async Task EnsureBaseRolesAsync()
        {
            foreach (var roleName in AllowedRoles)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }
        }

        [HttpGet("/Admin/Users")]
        [HttpGet("/{slug}/Admin/Users")]
        public async Task<IActionResult> Index(string? search, string? role, int page = 1)
        {
            await EnsureBaseRolesAsync();

            // Tüm kullanıcı-rol ilişkilerini tek sorguda çek (N+1 yok)
            var userRoleMap = await _context.UserRoles
                .AsNoTracking()
                .Join(_context.Roles.AsNoTracking(),
                    ur => ur.RoleId,
                    r => r.Id,
                    (ur, r) => new { ur.UserId, RoleName = r.Name })
                .Where(x => x.RoleName != null)
                .GroupBy(x => x.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.RoleName!).ToList());

            var users = await _userManager.Users
                .AsNoTracking()
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ThenBy(u => u.Email)
                .ToListAsync();

            var userIds = users.Select(u => u.Id).ToList();

            var loginStats = await _context.AuditLogs
                .AsNoTracking()
                .Where(a => a.Category == "Login" && a.Action == "Login" && a.UserId != null && userIds.Contains(a.UserId))
                .GroupBy(a => a.UserId!)
                .Select(g => new
                {
                    UserId = g.Key,
                    LastLoginAt = g.Max(a => a.CreatedAt),
                    LoginCount = g.Count(),
                    LastLoginIp = g.OrderByDescending(a => a.CreatedAt).Select(a => a.IpAddress).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.UserId);

            var allUsersModel = users.Select(user =>
            {
                var userRoles = userRoleMap.TryGetValue(user.Id, out var r) ? r : new List<string>();
                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                loginStats.TryGetValue(user.Id, out var stats);
                return new UserListItemViewModel
                {
                    UserId = user.Id,
                    Email = user.Email,
                    Name = string.IsNullOrWhiteSpace(fullName) ? user.Email ?? "-" : fullName,
                    Roles = userRoles
                        .Where(IsAllowedRole)
                        .OrderBy(roleName => roleName)
                        .ToList(),
                    IsLockedOut = user.LockoutEnabled &&
                                  user.LockoutEnd.HasValue &&
                                  user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                    EmailConfirmed = user.EmailConfirmed,
                    LastLoginAt = stats?.LastLoginAt,
                    LastLoginIp = stats?.LastLoginIp,
                    LoginCount = stats?.LoginCount ?? 0
                };
            }).ToList();

            var filteredModel = allUsersModel.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();

                filteredModel = filteredModel.Where(user =>
                    (!string.IsNullOrWhiteSpace(user.Name) &&
                     user.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(user.Email) &&
                     user.Email.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrWhiteSpace(role) &&
                !role.Equals("All", StringComparison.OrdinalIgnoreCase) &&
                IsAllowedRole(role))
            {
                filteredModel = filteredModel.Where(user =>
                    user.Roles != null &&
                    user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase));
            }

            ViewBag.Search = search;
            ViewBag.SelectedRole = role;
            ViewBag.AllowedRoles = AllowedRoles;

            ViewBag.TotalUserCount = allUsersModel.Count; // istatistik için filtre öncesi toplam

            ViewBag.SuperAdminCount = allUsersModel.Count(user =>
                user.Roles != null &&
                user.Roles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase));

            ViewBag.AdminCount = allUsersModel.Count(user =>
                user.Roles != null &&
                user.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase));

            ViewBag.AuthorCount = allUsersModel.Count(user =>
                user.Roles != null &&
                user.Roles.Contains("Author", StringComparer.OrdinalIgnoreCase));

            ViewBag.ListenerCount = allUsersModel.Count(user =>
                user.Roles != null &&
                user.Roles.Contains("Listener", StringComparer.OrdinalIgnoreCase));

            ViewBag.RefereeCount = allUsersModel.Count(user =>
                user.Roles != null &&
                user.Roles.Contains("Referee", StringComparer.OrdinalIgnoreCase));

            const int pageSize = 50;
            if (page < 1) page = 1;
            var filtered = filteredModel.ToList();
            ViewBag.FilteredCount = filtered.Count;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)filtered.Count / pageSize);

            return View(filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList());
        }

        [HttpGet("/Admin/Users/ManageRoles")]
        [HttpGet("/{slug}/Admin/Users/ManageRoles")]
        public async Task<IActionResult> ManageRoles(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return NotFound();
            }

            await EnsureBaseRolesAsync();

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            var model = new UserWithRolesViewModel
            {
                UserId = user.Id,
                UserEmail = user.Email ?? "",
                Roles = AllowedRoles
                    .OrderBy(role => role)
                    .Select(role => new UserWithRoleViewModel
                    {
                        RoleName = role,
                        IsSelected = userRoles.Contains(role, StringComparer.OrdinalIgnoreCase)
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpPost("/Admin/Users/ManageRoles")]
        [HttpPost("/{slug}/Admin/Users/ManageRoles")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageRoles(UserWithRolesViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.UserId))
            {
                return BadRequest();
            }

            await EnsureBaseRolesAsync();

            model.Roles ??= new List<UserWithRoleViewModel>();

            var targetUser = await _userManager.FindByIdAsync(model.UserId);

            if (targetUser == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return Challenge();
            }

            var existingRoles = await _userManager.GetRolesAsync(targetUser);

            var selectedRoles = model.Roles
                .Where(x => x.IsSelected && !string.IsNullOrWhiteSpace(x.RoleName))
                .Select(x => x.RoleName.Trim())
                .Where(IsAllowedRole)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (targetUser.Id == currentUser.Id &&
                !selectedRoles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = T(
                    "Error_CannotRemoveOwnSuperAdminRole",
                    "Kendi SuperAdmin rolünüzü kaldıramazsınız.");

                model.UserEmail = targetUser.Email ?? "";
                model.Roles = AllowedRoles
                    .OrderBy(role => role)
                    .Select(role => new UserWithRoleViewModel
                    {
                        RoleName = role,
                        IsSelected = existingRoles.Contains(role, StringComparer.OrdinalIgnoreCase)
                    })
                    .ToList();

                return View(model);
            }

            var existingAllowedRoles = existingRoles
                .Where(IsAllowedRole)
                .ToList();

            var rolesToRemove = existingAllowedRoles
                .Where(role => !selectedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var rolesToAdd = selectedRoles
                .Where(role => !existingAllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (rolesToRemove.Any())
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(targetUser, rolesToRemove);

                if (!removeResult.Succeeded)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_RemoveRoleFailed",
                        "Rol kaldırılırken bir hata oluştu.");

                    model.UserEmail = targetUser.Email ?? "";
                    return View(model);
                }
            }

            if (rolesToAdd.Any())
            {
                var addResult = await _userManager.AddToRolesAsync(targetUser, rolesToAdd);

                if (!addResult.Succeeded)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_AssignRoleFailed",
                        "Rol atanırken bir hata oluştu.");

                    model.UserEmail = targetUser.Email ?? "";
                    return View(model);
                }
            }

            TempData["SuccessMessage"] = T(
                "Success_RolesUpdated",
                "Kullanıcı rolleri başarıyla güncellendi.");

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("/Admin/Users/AssignRole")]
        [HttpPost("/{slug}/Admin/Users/AssignRole")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(string userId, string roleName)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ErrorMessage"] = T(
                    "Error_MissingUserOrRole",
                    "Kullanıcı veya rol bilgisi eksik.");

                return RedirectToAction(nameof(Index));
            }

            roleName = roleName.Trim();

            if (!IsAllowedRole(roleName))
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidRole",
                    "Geçersiz rol seçimi.");

                return RedirectToAction(nameof(Index));
            }

            await EnsureBaseRolesAsync();

            var targetUser = await _userManager.FindByIdAsync(userId);

            if (targetUser == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UserNotFound",
                    "Kullanıcı bulunamadı.");

                return RedirectToAction(nameof(Index));
            }

            if (!await _userManager.IsInRoleAsync(targetUser, roleName))
            {
                var addResult = await _userManager.AddToRoleAsync(targetUser, roleName);

                if (!addResult.Succeeded)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_RoleAssignmentFailed",
                        "Rol atanırken bir hata oluştu.");

                    return RedirectToAction(nameof(Index));
                }

                var adminUser = await _userManager.GetUserAsync(User);
                await _audit.LogAsync(
                    category: "RoleChange",
                    action: "RoleAdded",
                    userId: adminUser?.Id,
                    userName: adminUser != null ? $"{adminUser.FirstName} {adminUser.LastName}".Trim() : null,
                    entityType: "AppUser",
                    entityId: targetUser.Id,
                    description: $"{targetUser.Email} kullanıcısına '{roleName}' rolü atandı.",
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
            }

            TempData["SuccessMessage"] = T(
                "Success_RoleAssigned",
                "Rol başarıyla atandı.");

            return RedirectToAction(nameof(Index));
        }

        // ── Details ──────────────────────────────────────────────────────────────

        [HttpGet("/Admin/Users/Details/{userId}")]
        [HttpGet("/{slug}/Admin/Users/Details/{userId}")]
        public async Task<IActionResult> Details(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return NotFound();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var submissionCount = await _context.Submissions
                .AsNoTracking()
                .CountAsync(s => s.UserId == userId);

            var paymentCount = await _context.Payments
                .AsNoTracking()
                .CountAsync(p => p.AppUserId == userId);

            var certCount = await _context.Certificates
                .AsNoTracking()
                .CountAsync(c => c.UserId == userId);

            ViewBag.User = user;
            ViewBag.Roles = roles.Where(IsAllowedRole).ToList();
            ViewBag.SubmissionCount = submissionCount;
            ViewBag.PaymentCount = paymentCount;
            ViewBag.CertCount = certCount;
            ViewBag.IsLockedOut = user.LockoutEnabled &&
                                   user.LockoutEnd.HasValue &&
                                   user.LockoutEnd.Value > DateTimeOffset.UtcNow;

            return View();
        }

        // ── Login History ─────────────────────────────────────────────────────────

        [HttpGet("/Admin/Users/LoginHistory")]
        public async Task<IActionResult> LoginHistory(string? userId, int page = 1)
        {
            const int pageSize = 50;

            var query = _context.AuditLogs
                .AsNoTracking()
                .Where(a => a.Category == "Login" && a.Action == "Login");

            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(a => a.UserId == userId);

            var total = await query.CountAsync();

            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new { a.UserId, a.UserName, a.CreatedAt, a.IpAddress, a.Description })
                .ToListAsync();

            ViewBag.Logs = logs;
            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.FilterUserId = userId;

            return View();
        }

        // ── Lock / Unlock ─────────────────────────────────────────────────────────

        [HttpPost("/Admin/Users/Lock")]
        [HttpPost("/{slug}/Admin/Users/Lock")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var targetUser = await _userManager.FindByIdAsync(userId);

            if (targetUser == null)
            {
                TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            if (currentUser?.Id == targetUser.Id)
            {
                TempData["ErrorMessage"] = "Kendi hesabınızı kilitleyemezsiniz.";
                return RedirectToAction(nameof(Index));
            }

            await _userManager.SetLockoutEnabledAsync(targetUser, true);
            await _userManager.SetLockoutEndDateAsync(targetUser, DateTimeOffset.UtcNow.AddYears(100));

            TempData["SuccessMessage"] = $"{targetUser.Email} hesabı askıya alındı.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("/Admin/Users/Unlock")]
        [HttpPost("/{slug}/Admin/Users/Unlock")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string userId)
        {
            var targetUser = await _userManager.FindByIdAsync(userId);

            if (targetUser == null)
            {
                TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            await _userManager.SetLockoutEndDateAsync(targetUser, null);
            await _userManager.ResetAccessFailedCountAsync(targetUser);

            TempData["SuccessMessage"] = $"{targetUser.Email} hesabı yeniden aktif edildi.";
            return RedirectToAction(nameof(Index));
        }

        // ── Admin Şifre Sıfırlama ─────────────────────────────────────────────────

        [HttpPost("/Admin/Users/ResetPassword")]
        [HttpPost("/{slug}/Admin/Users/ResetPassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string userId, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["ErrorMessage"] = "Kullanıcı ID veya şifre boş olamaz.";
                return RedirectToAction(nameof(Index));
            }

            var targetUser = await _userManager.FindByIdAsync(userId);
            if (targetUser == null)
            {
                TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(targetUser);
            var result = await _userManager.ResetPasswordAsync(targetUser, token, newPassword);

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "Şifre sıfırlanamadı: " +
                    string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = $"{targetUser.Email} şifresi başarıyla sıfırlandı.";
            return RedirectToAction(nameof(Index));
        }

        // ── E-posta Doğrulama Manuel Onayla ──────────────────────────────────────

        [HttpPost("/Admin/Users/ConfirmEmail")]
        [HttpPost("/{slug}/Admin/Users/ConfirmEmail")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmEmailManual(string userId)
        {
            var targetUser = await _userManager.FindByIdAsync(userId);
            if (targetUser == null)
            {
                TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            if (!targetUser.EmailConfirmed)
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(targetUser);
                await _userManager.ConfirmEmailAsync(targetUser, token);
                TempData["SuccessMessage"] = $"{targetUser.Email} e-postası manuel olarak doğrulandı.";
            }
            else
            {
                TempData["InfoMessage"] = "Bu kullanıcının e-postası zaten doğrulanmış.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("/Admin/Users/RemoveRole")]
        [HttpPost("/{slug}/Admin/Users/RemoveRole")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRole(string userId, string roleName)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ErrorMessage"] = T(
                    "Error_MissingUserOrRole",
                    "Kullanıcı veya rol bilgisi eksik.");

                return RedirectToAction(nameof(Index));
            }

            roleName = roleName.Trim();

            if (!IsAllowedRole(roleName))
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidRole",
                    "Geçersiz rol seçimi.");

                return RedirectToAction(nameof(Index));
            }

            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return Challenge();
            }

            var targetUser = await _userManager.FindByIdAsync(userId);

            if (targetUser == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UserNotFound",
                    "Kullanıcı bulunamadı.");

                return RedirectToAction(nameof(Index));
            }

            if (targetUser.Id == currentUser.Id &&
                roleName.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = T(
                    "Error_CannotRemoveOwnSuperAdminRole",
                    "Kendi SuperAdmin rolünüzü kaldıramazsınız.");

                return RedirectToAction(nameof(Index));
            }

            if (await _userManager.IsInRoleAsync(targetUser, roleName))
            {
                var removeResult = await _userManager.RemoveFromRoleAsync(targetUser, roleName);

                if (!removeResult.Succeeded)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_RoleRemovalFailed",
                        "Rol kaldırılırken bir hata oluştu.");

                    return RedirectToAction(nameof(Index));
                }

                var adminUser = await _userManager.GetUserAsync(User);
                await _audit.LogAsync(
                    category: "RoleChange",
                    action: "RoleRemoved",
                    userId: adminUser?.Id,
                    userName: adminUser != null ? $"{adminUser.FirstName} {adminUser.LastName}".Trim() : null,
                    entityType: "AppUser",
                    entityId: targetUser.Id,
                    description: $"{targetUser.Email} kullanıcısından '{roleName}' rolü kaldırıldı.",
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
            }

            TempData["SuccessMessage"] = T(
                "Success_RoleRemoved",
                "Rol başarıyla kaldırıldı.");

            return RedirectToAction(nameof(Index));
        }
    }
}