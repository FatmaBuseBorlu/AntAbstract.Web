using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")] 
    public class SystemParametersController : Controller
    {
        private readonly AppDbContext _context;

        public SystemParametersController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var parameters = await _context.SystemParameters
                .AsNoTracking()
                .OrderBy(x => x.Group)
                .ThenBy(x => x.Order)
                .ThenBy(x => x.Name)
                .ToListAsync();

            return View(parameters);
        }

        [HttpGet]
        public IActionResult Create(string group = "University")
        {
            var model = new SystemParameter
            {
                Group = group,
                IsActive = true,
                Order = 0
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SystemParameter model)
        {
            if (ModelState.IsValid)
            {
                _context.SystemParameters.Add(model);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Parametre başarıyla eklendi.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var param = await _context.SystemParameters.FindAsync(id);
            if (param != null)
            {
                _context.SystemParameters.Remove(param);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Kayıt başarıyla silindi.";
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
                TempData["SuccessMessage"] = param.IsActive ? "Kayıt aktif edildi." : "Kayıt pasife alındı.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}