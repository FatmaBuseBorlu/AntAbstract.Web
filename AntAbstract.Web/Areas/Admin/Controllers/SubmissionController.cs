using AntAbstract.Application.DTOs.Submission;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Editor,Moderator")] 
    public class SubmissionController : Controller
    {
        private readonly ISubmissionService _submissionService;

        public SubmissionController(ISubmissionService submissionService)
        {
            _submissionService = submissionService;
        }

        public async Task<IActionResult> Index()
        {
            var submissions = await _submissionService.GetAllSubmissionsAsync();
            return View(submissions);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var submission = await _submissionService.GetSubmissionByIdAsync(id);
            if (submission == null) return NotFound();

            return View(submission);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(Guid id, string status)
        {
            if (Enum.TryParse<SubmissionStatus>(status, out var newStatus))
            {
                await _submissionService.UpdateStatusAsync(id, newStatus);
                TempData["SuccessMessage"] = "Bildiri durumu güncellendi: " + status;
            }
            else
            {
                TempData["ErrorMessage"] = "Geçersiz durum bilgisi.";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _submissionService.DeleteSubmissionAsync(id);
            TempData["SuccessMessage"] = "Bildiri sistemden tamamen silindi.";
            return RedirectToAction("Index");
        }
        
    }
}