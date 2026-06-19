using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
    public class PermissionMatrixController : Controller
    {
        [HttpGet("/{slug}/Admin/PermissionMatrix")]
        [HttpGet("/Admin/PermissionMatrix")]
        public IActionResult Index(string? slug)
        {
            ViewBag.Slug = slug ?? "";
            return View();
        }
    }
}
