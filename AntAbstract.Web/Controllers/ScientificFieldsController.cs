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
    public class ScientificFieldsController : Controller
    {
        private readonly AppDbContext _context;

        public ScientificFieldsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.ScientificFields.ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var scientificField = await _context.ScientificFields
                .FirstOrDefaultAsync(m => m.Id == id);
            if (scientificField == null)
            {
                return NotFound();
            }

            return View(scientificField);
        }
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name")] ScientificField scientificField)
        {
            if (ModelState.IsValid)
            {
                _context.Add(scientificField);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(scientificField);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var scientificField = await _context.ScientificFields.FindAsync(id);
            if (scientificField == null)
            {
                return NotFound();
            }
            return View(scientificField);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] ScientificField scientificField)
        {
            if (id != scientificField.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(scientificField);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ScientificFieldExists(scientificField.Id))
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
            return View(scientificField);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var scientificField = await _context.ScientificFields
                .FirstOrDefaultAsync(m => m.Id == id);
            if (scientificField == null)
            {
                return NotFound();
            }

            return View(scientificField);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var scientificField = await _context.ScientificFields.FindAsync(id);
            if (scientificField != null)
            {
                _context.ScientificFields.Remove(scientificField);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ScientificFieldExists(int id)
        {
            return _context.ScientificFields.Any(e => e.Id == id);
        }
    }
}
