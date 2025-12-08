using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;

namespace AntAbstract.Web.Controllers
{
    public class CongressTypesController : Controller
    {
        private readonly AppDbContext _context;

        public CongressTypesController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.CongressTypes.ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var congressType = await _context.CongressTypes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (congressType == null)
            {
                return NotFound();
            }

            return View(congressType);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name")] CongressType congressType)
        {
            if (ModelState.IsValid)
            {
                _context.Add(congressType);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(congressType);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var congressType = await _context.CongressTypes.FindAsync(id);
            if (congressType == null)
            {
                return NotFound();
            }
            return View(congressType);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] CongressType congressType)
        {
            if (id != congressType.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(congressType);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CongressTypeExists(congressType.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(congressType);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var congressType = await _context.CongressTypes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (congressType == null)
            {
                return NotFound();
            }

            return View(congressType);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var congressType = await _context.CongressTypes.FindAsync(id);
            if (congressType != null)
            {
                _context.CongressTypes.Remove(congressType);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CongressTypeExists(int id)
        {
            return _context.CongressTypes.Any(e => e.Id == id);
        }
    }
}
