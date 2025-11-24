using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using AntAbstract.Web.Models;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

        // 1. LİSTELEME SAYFASI (Dashboard'daki "Bildirilerim" linki buraya gelecek)
        public IActionResult Index()
        {
            // Şimdilik boş döndürelim, kayıttan sonra burayı dolduracağız.
            return View();
        }

        // 2. EKLEME SAYFASI (FORMU GÖSTER)
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // 3. EKLEME İŞLEMİ (FORMU KAYDET)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubmissionCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);

                // --- Dosya Yükleme İşlemi ---
                string uniqueFileName = null;
                if (model.SubmissionFile != null)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "submissions");

                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    uniqueFileName = Guid.NewGuid().ToString() + "_" + model.SubmissionFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.SubmissionFile.CopyToAsync(fileStream);
                    }
                }

                // --- Veritabanı Nesnesini Oluştur ---
                var submission = new Submission
                {
                    Title = model.Title,
                    AbstractText = model.AbstractText,
                    Keywords = model.Keywords,
                    AuthorId = user.Id, // Giriş yapan kullanıcı
                    CreatedAt = DateTime.UtcNow,
                    Status = SubmissionStatus.New, // Enum: Yeni
                    // Dosya işlemleri için SubmissionFile tablosunu kullanacağız demiştik ama
                    // hızlı başlangıç için şimdilik manuel ekliyoruz, sonra servise taşırız.
                };

                // Dosya kaydını ilişkisel tabloya ekleyelim (Domain yapınıza göre)
                var subFile = new SubmissionFile
                {
                    FileName = model.SubmissionFile.FileName,
                    StoredFileName = uniqueFileName,
                    FilePath = "/uploads/submissions/" + uniqueFileName,
                    Type = SubmissionFileType.FullTextDoc,
                    UploadedAt = DateTime.UtcNow,
                    Version = 1,
                    Submission = submission // İlişkiyi kuruyoruz
                };

                submission.Files.Add(subFile);

                _context.Submissions.Add(submission);
                await _context.SaveChangesAsync();

                // Başarılı olursa Dashboard'a veya Listeye dön
                return RedirectToAction("Index", "Dashboard");
            }

            return View(model);
        }
    }
}