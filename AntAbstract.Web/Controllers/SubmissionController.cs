using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context; // Context adınızı kontrol edin
using AntAbstract.Web.Models;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize] // Sadece üye olanlar bildiri gönderebilir
    public class SubmissionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _env; // Dosya kaydetmek için

        public SubmissionController(AppDbContext context, UserManager<AppUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        // SubmissionController.cs içindeki Index() metodunun yeni hali:

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            // Yalnızca giriş yapan kullanıcının bildirilerini çekiyoruz
            var userSubmissions = await _context.Submissions
                .Where(s => s.AuthorId == user.Id)
                // Yazar bilgilerini ve varsa dosya bilgisini de dahil edebiliriz
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.Files)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var viewModel = new SubmissionListViewModel
            {
                Submissions = userSubmissions // <-- Eşitliğin sağındaki "userSubmissions" listeyi taşır.
            };

            return View(viewModel);
        }

        // 2. EKLEME SAYFASI (FORMU GÖSTER)
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // SubmissionController.cs içindeki Create(POST) metodunun güncellenmiş kısmı:

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubmissionCreateViewModel model)
        {
            // Dosya yükleme zorunluluğunu kontrol etmek için özel mantık
            if (model.SubmissionFile == null)
            {
                ModelState.AddModelError("SubmissionFile", "Lütfen bildiri dosyanızı yükleyiniz.");
            }

            if (model.Authors == null || !model.Authors.Any())
            {
                ModelState.AddModelError("Authors", "En az bir ortak yazar (siz dahil) eklemelisiniz.");
            }

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);

                // --- Dosya Yükleme İşlemi --- (Bu kısım sizde zaten var, dokunmuyoruz)
                string uniqueFileName = null;
                if (model.SubmissionFile != null)
                {
                    // ... (Dosya kaydetme mantığınız) ...
                    // uniqueFileName, filePath değişkenleri burada atanır.
                }

                // --- Veritabanı Nesnesini Oluştur ---
                var submission = new Submission
                {
                    Title = model.Title,
                    AbstractText = model.AbstractText,
                    Keywords = model.Keywords,
                    AuthorId = user.Id,
                    CreatedAt = DateTime.UtcNow,
                    Status = SubmissionStatus.New,
                    // ⚠️ Conference ID'si burada mutlaka atanmalıdır!
                    // Örn: ConferenceId = _context.Conferences.FirstOrDefault().Id
                };

                // --- YENİ EKLENEN KRİTİK KISIM: ORTAK YAZARLARI KAYDETME ---
                if (model.Authors != null)
                {
                    int authorOrder = 1;
                    foreach (var authorVm in model.Authors)
                    {
                        submission.SubmissionAuthors.Add(new SubmissionAuthor
                        {
                            FirstName = authorVm.FirstName,
                            LastName = authorVm.LastName,
                            Email = authorVm.Email,
                            Institution = authorVm.Institution,
                            ORCID = authorVm.ORCID,
                            IsCorrespondingAuthor = authorVm.IsCorrespondingAuthor,
                            Order = authorOrder++ // Sıralamayı otomatik veriyoruz
                        });
                    }
                }
                // -----------------------------------------------------------------

                // Dosya kaydını ilişkisel tabloya ekleyelim (Eski kodunuzdan geldi)
                var subFile = new SubmissionFile { /* ... Dosya verileri ... */ };
                submission.Files.Add(subFile); // Bu satırın hemen öncesinde dosya kaydı yapılmalıdır.

                _context.Submissions.Add(submission);
                // [SubmissionAuthors], [SubmissionFiles] ilişkileri otomatik olarak kaydedilir.
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Bildiriniz başarıyla sisteme kaydedildi!";
                return RedirectToAction("Index", "Dashboard");
            }

            return View(model);
        }
    }
}