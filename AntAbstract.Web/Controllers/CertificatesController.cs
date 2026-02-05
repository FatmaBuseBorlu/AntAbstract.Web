using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class CertificatesController : Controller
    {
        private readonly ICertificateService _certificateService;
        private readonly UserManager<AppUser> _userManager;

        public CertificatesController(ICertificateService certificateService, UserManager<AppUser> userManager)
        {
            _certificateService = certificateService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var certs = await _certificateService.GetMyCertificatesAsync(user.Id);
            return View(certs); 
        }

        [HttpGet]
        public async Task<IActionResult> Download(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var bytes = await _certificateService.GetCertificateFileAsync(id, user.Id);
            if (bytes == null) return NotFound("Sertifika bulunamadı.");

            return File(bytes, "application/pdf", $"certificate_{id}.pdf");
        }
    }
}
