using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class CertificatesController : Controller
    {
        private readonly ICertificateService _certificateService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IStringLocalizer<CertificatesController> _localizer;

        public CertificatesController(
            ICertificateService certificateService,
            UserManager<AppUser> userManager,
            IStringLocalizer<CertificatesController> localizer)
        {
            _certificateService = certificateService;
            _userManager = userManager;
            _localizer = localizer;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound
                ? fallback
                : value.Value;
        }

        [HttpGet("/Certificates")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var certificates = await _certificateService.GetMyCertificatesAsync(user.Id);

            return View(certificates);
        }

        [HttpGet("/Certificates/Download/{id:guid}")]
        public async Task<IActionResult> Download(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest(T(
                    "Error_InvalidCertificate",
                    "Geçersiz sertifika isteği."));
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var bytes = await _certificateService.GetCertificateFileAsync(id, user.Id);

            if (bytes == null || bytes.Length == 0)
            {
                return NotFound(T(
                    "CertificateNotFound",
                    "Sertifika bulunamadı veya bu sertifikayı indirme yetkiniz yok."));
            }

            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return File(
                bytes,
                "application/pdf",
                $"certificate_{id}.pdf");
        }
    }
}