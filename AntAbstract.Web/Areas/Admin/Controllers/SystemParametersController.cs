using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
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
                .AnyAsync(x =>
                    x.Group == model.Group &&
                    x.Name.ToLower() == model.Name.ToLower());

            if (exists)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    "Bu kayıt aynı grup içinde zaten mevcut.");

                return View(model);
            }

            _context.SystemParameters.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = _localizer["Success_ParameterAdded"].Value;

            return RedirectToAction(nameof(Index), new { group = model.Group });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
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
            if (id != model.Id)
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
                .AnyAsync(x =>
                    x.Id != id &&
                    x.Group == model.Group &&
                    x.Name.ToLower() == model.Name.ToLower());

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    "Bu kayıt aynı grup içinde zaten mevcut.");

                return View(model);
            }

            parameter.Group = model.Group;
            parameter.Name = model.Name;
            parameter.NameEn = model.NameEn;
            parameter.Order = model.Order;
            parameter.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = _localizer["Success_RecordUpdated"].Value;

            return RedirectToAction(nameof(Index), new { group = parameter.Group });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var param = await _context.SystemParameters.FindAsync(id);

            if (param != null)
            {
                var group = param.Group;

                _context.SystemParameters.Remove(param);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = _localizer["Success_RecordDeleted"].Value;

                return RedirectToAction(nameof(Index), new { group });
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var param = await _context.SystemParameters.FindAsync(id);

            if (param != null)
            {
                param.IsActive = !param.IsActive;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = param.IsActive
                    ? _localizer["Success_RecordActivated"].Value
                    : _localizer["Success_RecordDeactivated"].Value;

                return RedirectToAction(nameof(Index), new { group = param.Group });
            }

            return RedirectToAction(nameof(Index));
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
                : string.Join(" ", value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}