using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Documents; // Sertifika şablonumuzun olduğu klasör
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent; // PDF üretmek için bu using gerekli

namespace AntAbstract.Web.Controllers
{
    public class CertificateController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public CertificateController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        // Test amaçlı bir PDF sertifikası üreten metot
        public IActionResult GenerateTestCertificate()
        {
            // 1. Şablon için test verilerini hazırlıyoruz.
            //    Normalde bu verileri veritabanından çekeceğiz.
            string recipientName = "Test Kullanıcısı";
            string conferenceName = _tenantContext.Current?.Name ?? "Test Konferansı";

            // 2. Sertifika şablonumuzu bu verilerle oluşturuyoruz.
            var document = new CertificateDocument(recipientName, conferenceName);

            // 3. QuestPDF kütüphanesini kullanarak PDF'i bir byte dizisi olarak üretiyoruz.
            byte[] pdfBytes = document.GeneratePdf();

            // 4. Üretilen PDF'i, tarayıcının indirebilmesi için bir dosya olarak geri döndürüyoruz.
            return File(pdfBytes, "application/pdf", "sertifika.pdf");
        }
        public async Task<IActionResult> GenerateForAllAcceptedAuthors()
        {
            // 1. O anki kongre (tenant) için ilgili Conference nesnesini bul.
            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                return Content("Bu kongre için bir etkinlik bulunamadı.");
            }

            // 2. Bu konferansta, kararı "Kabul Edildi" olan ve yazarı bulunan tüm özetleri bul.
            //    Include(s => s.Author) -> Yazar bilgilerini de sorguya dahil et.
            var acceptedSubmissions = await _context.Submissions
                .Include(s => s.Author)
                .Where(s => s.ConferenceId == conference.Id &&
                            s.FinalDecision == "Kabul Edildi" &&
                            s.Author != null)
                .ToListAsync();

            // 3. Sertifika üretilecek kimse yoksa kullanıcıyı bilgilendir.
            if (!acceptedSubmissions.Any())
            {
                return Content("Sertifika üretilecek, sunumu kabul edilmiş bir yazar bulunamadı.");
            }

            // 4. Toplu sertifika şablonumuzu, bulduğumuz özet listesiyle oluştur.
            var document = new CertificateCollectionDocument(acceptedSubmissions, conference.Title);

            // 5. PDF'i üret.
            byte[] pdfBytes = document.GeneratePdf();

            // 6. Üretilen çok sayfalı PDF'i, kongreye özel bir isimle kullanıcıya indir.
            string fileName = $"Katilim_Sertifikalari_{_tenantContext.Current.Slug}_{DateTime.Now:yyyy-MM-dd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}