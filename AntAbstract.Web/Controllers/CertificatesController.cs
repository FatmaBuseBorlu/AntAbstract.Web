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

        [HttpGet("/{slug}/Certificates/My")]
        public async Task<IActionResult> My(string slug, Guid? conferenceId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var list = await _certificateService.GetMyCertificatesAsync(user.Id, conferenceId);
            return View("~/Views/Certificates/My.cshtml", list);
        }

        [HttpGet("/{slug}/Certificates/Download/{id:guid}")]
        public async Task<IActionResult> Download(string slug, Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var bytes = await _certificateService.GetCertificateFileAsync(id, user.Id);
            if (bytes == null) return NotFound();

            return File(bytes, "application/pdf", "certificate.pdf");
        }
    }
}
