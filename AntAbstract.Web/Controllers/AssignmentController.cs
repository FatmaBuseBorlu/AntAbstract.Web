using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Admin,Organizator")]
    public class AssignmentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public AssignmentController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        // GET: Assignment/Index (Bu metot sende zaten vardı)
        public async Task<IActionResult> Index()
        {
            if (_tenantContext.Current == null)
            {
                TempData["ErrorMessage"] = "Lütfen bir kongre URL'i (/slug) üzerinden işlem yapın.";
                return RedirectToAction("Index", "Home");
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                ViewBag.ErrorMessage = "Bu kongre için veritabanında bir konferans kaydı bulunamadı.";
                return View(new List<Domain.Entities.Submission>());
            }

            var submissions = await _context.Submissions
                .Where(s => s.ConferenceId == conference.Id)
                .Include(s => s.Author)
                .ToListAsync();

            return View(submissions);
        }

        // ------------------ ✅ YENİ METOTLARI BURAYA YAPIŞTIR ------------------

        // GET: Assignment/Assign/5
        // Belirli bir özete hakem atama formunu gösterir.
        public async Task<IActionResult> Assign(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var submission = await _context.Submissions
                .Include(s => s.Author)
                .FirstOrDefaultAsync(m => m.SubmissionId == id);

            if (submission == null)
            {
                return NotFound();
            }

            var reviewers = await _context.Reviewers
                .Where(r => r.ConferenceId == submission.ConferenceId && r.IsActive)
                .Include(r => r.AppUser)
                .ToListAsync();

            ViewBag.ReviewerId = new SelectList(reviewers, "Id", "AppUser.Email");

            return View(submission);
        }

        // POST: Assignment/Assign
        // Formdan gelen bilgilerle yeni bir hakem ataması oluşturur.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(Guid submissionId, Guid reviewerId)
        {
            if (submissionId == Guid.Empty || reviewerId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "Özet veya Hakem seçimi geçersiz.";
                return RedirectToAction(nameof(Index));
            }

            var existingAssignment = await _context.ReviewAssignments
                .AnyAsync(a => a.SubmissionId == submissionId && a.ReviewerId == reviewerId);

            if (existingAssignment)
            {
                TempData["ErrorMessage"] = "Bu hakem bu özete zaten atanmış.";
                return RedirectToAction("Assign", new { id = submissionId });
            }

            var newAssignment = new ReviewAssignment
            {
                ReviewAssignmentId = Guid.NewGuid(),
                SubmissionId = submissionId,
                ReviewerId = reviewerId,
                AssignedDate = DateTime.UtcNow,
                Status = "Atandı",
                DueDate = DateTime.UtcNow.AddDays(14)
            };

            _context.ReviewAssignments.Add(newAssignment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Atama başarıyla yapıldı.";
            return RedirectToAction(nameof(Index));
        }

    } // <- Controller sınıfının kapanış parantezi. Tüm metotlar bunun içinde olmalı.
}