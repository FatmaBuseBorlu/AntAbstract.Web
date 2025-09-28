using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Http;
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
            return View(await _context.Tenants.ToListAsync());
        }

        // GET: Tenants/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tenant = await _context.Tenants
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
            // View'a gönderilecek dropdown listelerini hazırla
            ViewBag.ScientificFieldId = new SelectList(_context.ScientificFields, "Id", "Name");
            ViewBag.CongressTypeId = new SelectList(_context.CongressTypes, "Id", "Name");
            return View();
        }

        // POST: Tenants/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IFormCollection collection)
        {
            try
            {
                // 1. Veriyi formdan manuel olarak oku (yeni alanlar dahil)
                string slug = collection["Slug"];
                string name = collection["Name"];

                // Gelen ID'leri int'e çevirmeye çalış, boşsa null ata
                int? scientificFieldId = !string.IsNullOrEmpty(collection["ScientificFieldId"]) ? int.Parse(collection["ScientificFieldId"]) : null;
                int? congressTypeId = !string.IsNullOrEmpty(collection["CongressTypeId"]) ? int.Parse(collection["CongressTypeId"]) : null;

                if (string.IsNullOrEmpty(slug) || string.IsNullOrEmpty(name))
                {
                    ModelState.AddModelError("", "Slug ve Name alanları boş olamaz.");
                    // Hata durumunda dropdown'ları tekrar doldurup formu geri gönder
                    ViewBag.ScientificFieldId = new SelectList(_context.ScientificFields, "Id", "Name");
                    ViewBag.CongressTypeId = new SelectList(_context.CongressTypes, "Id", "Name");
                    return View();
                }

                // 2. Tenant nesnesini manuel olarak oluştur
                var newTenant = new Tenant
                {
                    Slug = slug,
                    Name = name,
                    ScientificFieldId = scientificFieldId,
                    CongressTypeId = congressTypeId,
                    LogoUrl = collection["LogoUrl"],

                };

                _context.Add(newTenant);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
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
            ViewBag.ScientificFieldId = new SelectList(_context.ScientificFields, "Id", "Name", tenant.ScientificFieldId);
            ViewBag.CongressTypeId = new SelectList(_context.CongressTypes, "Id", "Name", tenant.CongressTypeId);
            return View(tenant);
        }

        // POST: Tenants/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, IFormCollection collection)
        {
            var tenantToUpdate = await _context.Tenants.FindAsync(id);
            if (tenantToUpdate == null)
            {
                return NotFound();
            }

            // Formdan gelen yeni verileri manuel olarak oku ve ata
            tenantToUpdate.Slug = collection["Slug"];
            tenantToUpdate.Name = collection["Name"];
            tenantToUpdate.ScientificFieldId = !string.IsNullOrEmpty(collection["ScientificFieldId"]) ? int.Parse(collection["ScientificFieldId"]) : null;
            tenantToUpdate.CongressTypeId = !string.IsNullOrEmpty(collection["CongressTypeId"]) ? int.Parse(collection["CongressTypeId"]) : null;
            tenantToUpdate.LogoUrl = collection["LogoUrl"];

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // ... (hata yönetimi) ...
                throw;
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Tenants/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tenant = await _context.Tenants
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