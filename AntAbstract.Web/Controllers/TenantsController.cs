using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    // TODO: Bu controller'a daha sonra Admin/Organizator yetkisi ekleyeceğiz
    public class TenantsController : Controller
    {
        private readonly AppDbContext _context;

        public TenantsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Tenants
        public async Task<IActionResult> Index()
        {
            // İlgili Bilimsel Alan ve Kongre Türü isimlerini de getirmek için Include kullanıyoruz.
            var tenants = _context.Tenants
                .Include(t => t.ScientificField)
                .Include(t => t.CongressType);
            return View(await tenants.ToListAsync());
        }

        // GET: Tenants/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tenant = await _context.Tenants
                .Include(t => t.ScientificField)
                .Include(t => t.CongressType)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (tenant == null)
            {
                return NotFound();
            }

            return View(tenant);
        }

        // GET: Tenants/Create
        public IActionResult Create()
        {
            // View'a gönderilecek dropdown listelerini hazırla (isme göre sıralı)
            ViewBag.ScientificFieldId = new SelectList(_context.ScientificFields.OrderBy(s => s.Name), "Id", "Name");
            ViewBag.CongressTypeId = new SelectList(_context.CongressTypes.OrderBy(c => c.Name), "Id", "Name");
            return View();
        }

        // POST: Tenants/Create
        // GÜVENLİ VE DOĞRU VERSİYON: IFormCollection yerine Model Binding kullanılıyor.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Slug,LogoUrl,ScientificFieldId,CongressTypeId")] Tenant tenant)
        {
            if (ModelState.IsValid)
            {
                tenant.Id = Guid.NewGuid();
                _context.Add(tenant);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            // Hata durumunda dropdown'ları yeniden doldur ve formu geri gönder
            ViewBag.ScientificFieldId = new SelectList(_context.ScientificFields.OrderBy(s => s.Name), "Id", "Name", tenant.ScientificFieldId);
            ViewBag.CongressTypeId = new SelectList(_context.CongressTypes.OrderBy(c => c.Name), "Id", "Name", tenant.CongressTypeId);
            return View(tenant);
        }

        // GET: Tenants/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null)
            {
                return NotFound();
            }

            // View'a gönderilecek dropdown listelerini, mevcut seçimi de belirterek hazırla
            ViewBag.ScientificFieldId = new SelectList(_context.ScientificFields.OrderBy(s => s.Name), "Id", "Name", tenant.ScientificFieldId);
            ViewBag.CongressTypeId = new SelectList(_context.CongressTypes.OrderBy(c => c.Name), "Id", "Name", tenant.CongressTypeId);
            return View(tenant);
        }

        // POST: Tenants/Edit/5
        // GÜVENLİ VE DOĞRU VERSİYON: IFormCollection yerine Model Binding kullanılıyor.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name,Slug,LogoUrl,ScientificFieldId,CongressTypeId")] Tenant tenant)
        {
            if (id != tenant.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tenant);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TenantExists(tenant.Id))
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
            // Hata durumunda dropdown'ları yeniden doldur ve formu geri gönder
            ViewBag.ScientificFieldId = new SelectList(_context.ScientificFields.OrderBy(s => s.Name), "Id", "Name", tenant.ScientificFieldId);
            ViewBag.CongressTypeId = new SelectList(_context.CongressTypes.OrderBy(c => c.Name), "Id", "Name", tenant.CongressTypeId);
            return View(tenant);
        }

        // GET: Tenants/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tenant = await _context.Tenants
                .Include(t => t.ScientificField)
                .Include(t => t.CongressType)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (tenant == null)
            {
                return NotFound();
            }

            return View(tenant);
        }

        // POST: Tenants/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant != null)
            {
                _context.Tenants.Remove(tenant);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TenantExists(Guid id)
        {
            return _context.Tenants.Any(e => e.Id == id);
        }
    }
}