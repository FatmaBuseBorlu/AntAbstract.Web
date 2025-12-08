using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using AntAbstract.Web.Models.ViewModels;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Admin,Organizator")]
    public class ReviewFormsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public ReviewFormsController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        public async Task<IActionResult> Index()
        {
            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null) return RedirectToAction("Index", "Dashboard");

            var forms = await _context.ReviewForms
                .Where(f => f.ConferenceId == conference.Id)
                .ToListAsync();

            return View(forms);
        }

        public async Task<IActionResult> Create()
        {
            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null) return RedirectToAction(nameof(Index));

            var form = new ReviewForm { ConferenceId = conference.Id };
            return View(form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,ConferenceId")] ReviewForm reviewForm)
        {
            if (ModelState.IsValid)
            {
                reviewForm.Id = Guid.NewGuid();
                _context.Add(reviewForm);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Form başarıyla oluşturuldu. Şimdi forma kriter ekleyebilirsiniz.";
                return RedirectToAction(nameof(Index));
            }
            return View(reviewForm);
        }


        public async Task<IActionResult> ManageCriteria(Guid id)
        {
            var form = await _context.ReviewForms
                .Include(f => f.Criteria)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (form == null) return NotFound();

            var viewModel = new ManageCriteriaViewModel
            {
                Form = form,
                ExistingCriteria = form.Criteria.OrderBy(c => c.DisplayOrder).ToList(),
                NewCriterion = new ReviewCriterion { FormId = id },
                CriterionTypes = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Scale1To10", Text = "Puanlama (1-10)" },
                    new SelectListItem { Value = "Scale1To5", Text = "Puanlama (1-5)" },
                    new SelectListItem { Value = "FreeText", Text = "Yorum Metni" }
                }
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCriterion(ManageCriteriaViewModel model)
        {
            ModelState.Remove("Form");
            ModelState.Remove("ExistingCriteria");
            ModelState.Remove("CriterionTypes");

            if (ModelState.IsValid)
            {
                var newCriterion = model.NewCriterion;
                _context.ReviewCriteria.Add(newCriterion);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Yeni kriter başarıyla eklendi.";
            }
            else
            {
                TempData["ErrorMessage"] = "Kriter eklenirken bir hata oluştu. Lütfen hataları düzeltip tekrar deneyin.";
            }
            return RedirectToAction(nameof(ManageCriteria), new { id = model.NewCriterion.FormId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCriterion(Guid criterionId, Guid formId)
        {
            var criterionToDelete = await _context.ReviewCriteria.FindAsync(criterionId);
            if (criterionToDelete != null)
            {
                _context.ReviewCriteria.Remove(criterionToDelete);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Kriter başarıyla silindi.";
            }
            else
            {
                TempData["ErrorMessage"] = "Silinecek kriter bulunamadı.";
            }

            return RedirectToAction(nameof(ManageCriteria), new { id = formId });
        }

        public async Task<IActionResult> EditCriterion(Guid id) 
        {
            var criterion = await _context.ReviewCriteria.FindAsync(id);
            if (criterion == null) return NotFound();

            ViewBag.CriterionTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Scale1To10", Text = "Puanlama (1-10)" },
                new SelectListItem { Value = "Scale1To5", Text = "Puanlama (1-5)" },
                new SelectListItem { Value = "FreeText", Text = "Yorum Metni" }
            };

            return View(criterion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCriterion(Guid id, [Bind("Id,QuestionText,DisplayOrder,CriterionType,FormId")] ReviewCriterion criterion)
        {
            if (id != criterion.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(criterion);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Kriter başarıyla güncellendi.";
                return RedirectToAction(nameof(ManageCriteria), new { id = criterion.FormId });
            }

            ViewBag.CriterionTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Scale1To10", Text = "Puanlama (1-10)" },
                new SelectListItem { Value = "Scale1To5", Text = "Puanlama (1-5)" },
                new SelectListItem { Value = "FreeText", Text = "Yorum Metni" }
            };
            return View(criterion);
        }
    }
}