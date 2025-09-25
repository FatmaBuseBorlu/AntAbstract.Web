using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
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
            var tenants = await _context.Tenants.ToListAsync();
            return View(tenants);
        }

        // GET: Tenants/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Tenants/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Tenant tenant)
        {
            // DİKKAT: GEÇİCİ OLARAK ModelState KONTROLÜNÜ TAMAMEN KALDIRDIK
            try
            {
                // Modelin Id'si kendi içinde oluştuğu için direkt eklemeyi deniyoruz.
                _context.Add(tenant);
                await _context.SaveChangesAsync();

                // Başarılı olursa listeye yönlendir.
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // EĞER VERİTABANI KAYDI SIRASINDA BİR HATA VARSA, BURADA YAKALAYACAĞIZ
                // ve hatayı Output penceresine yazdıracağız.
                System.Diagnostics.Debug.WriteLine("KAYIT SIRASINDA VERİTABANI HATASI: " + ex.ToString());

                // Hata olursa, formu tekrar gösterelim ve kullanıcıya bilgi verelim.
                ModelState.AddModelError("", "Kayıt sırasında beklenmedik bir veritabanı hatası oluştu. Lütfen sistem yöneticisine başvurun.");
                return View(tenant);
            }
        }
    }
}