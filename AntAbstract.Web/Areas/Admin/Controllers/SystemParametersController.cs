using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin")]
    public class SystemParametersController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IStringLocalizer<SystemParametersController> _localizer;

        public SystemParametersController(
            AppDbContext context,
            IStringLocalizer<SystemParametersController> localizer)
        {
            _context = context;
            _localizer = localizer;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        public async Task<IActionResult> Index(string group = "University")
        {
            var parameters = await _context.SystemParameters
                .AsNoTracking()
                .OrderBy(x => x.Group)
                .ThenBy(x => x.Order)
                .ThenBy(x => x.Name)
                .ToListAsync();

            ViewBag.ActiveGroup = string.IsNullOrWhiteSpace(group)
                ? "University"
                : group;

            return View(parameters);
        }

        [HttpGet]
        public IActionResult Create(string group = "University")
        {
            var model = new SystemParameter
            {
                Group = NormalizeGroup(group),
                IsActive = true,
                Order = 0
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SystemParameter model)
        {
            NormalizeModel(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var exists = await _context.SystemParameters
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Group == model.Group &&
                    x.Name.ToLower() == model.Name.ToLower());

            if (exists)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    T("Error_DuplicateParameter", "Bu kayıt aynı grup içinde zaten mevcut."));

                return View(model);
            }

            _context.SystemParameters.Add(model);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_ParameterAdded",
                "Parametre başarıyla eklendi.");

            return RedirectToAction(nameof(Index), new
            {
                group = model.Group
            });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            var parameter = await _context.SystemParameters
                .FirstOrDefaultAsync(x => x.Id == id);

            if (parameter == null)
            {
                return NotFound();
            }

            return View(parameter);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SystemParameter model)
        {
            if (id <= 0 || id != model.Id)
            {
                return BadRequest();
            }

            NormalizeModel(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var parameter = await _context.SystemParameters
                .FirstOrDefaultAsync(x => x.Id == id);

            if (parameter == null)
            {
                return NotFound();
            }

            var duplicateExists = await _context.SystemParameters
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id != id &&
                    x.Group == model.Group &&
                    x.Name.ToLower() == model.Name.ToLower());

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    T("Error_DuplicateParameter", "Bu kayıt aynı grup içinde zaten mevcut."));

                return View(model);
            }

            parameter.Group = model.Group;
            parameter.Name = model.Name;
            parameter.NameEn = model.NameEn;
            parameter.Order = model.Order;
            parameter.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_RecordUpdated",
                "Kayıt başarıyla güncellendi.");

            return RedirectToAction(nameof(Index), new
            {
                group = parameter.Group
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
            {
                return RedirectToAction(nameof(Index));
            }

            var parameter = await _context.SystemParameters
                .FirstOrDefaultAsync(x => x.Id == id);

            if (parameter == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var group = parameter.Group;

            _context.SystemParameters.Remove(parameter);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_RecordDeleted",
                "Kayıt başarıyla silindi.");

            return RedirectToAction(nameof(Index), new
            {
                group
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            if (id <= 0)
            {
                return RedirectToAction(nameof(Index));
            }

            var parameter = await _context.SystemParameters
                .FirstOrDefaultAsync(x => x.Id == id);

            if (parameter == null)
            {
                return RedirectToAction(nameof(Index));
            }

            parameter.IsActive = !parameter.IsActive;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = parameter.IsActive
                ? T("Success_RecordActivated", "Kayıt aktif hale getirildi.")
                : T("Success_RecordDeactivated", "Kayıt pasif hale getirildi.");

            return RedirectToAction(nameof(Index), new
            {
                group = parameter.Group
            });
        }

        private static void NormalizeModel(SystemParameter model)
        {
            model.Group = NormalizeGroup(model.Group);
            model.Name = NormalizeText(model.Name);
            model.NameEn = string.IsNullOrWhiteSpace(model.NameEn)
                ? null
                : NormalizeText(model.NameEn);
        }

        private static string NormalizeGroup(string? group)
        {
            return string.IsNullOrWhiteSpace(group)
                ? "University"
                : group.Trim();
        }

        private static string NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : string.Join(
                    " ",
                    value.Trim().Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries));
        }
    }
}