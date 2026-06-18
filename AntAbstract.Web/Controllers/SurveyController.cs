using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Net;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class SurveyController : Controller
    {
        private const int MaxAnswerLength = 2000;

        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IStringLocalizer<SurveyController> _localizer;

        public SurveyController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            IStringLocalizer<SurveyController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _localizer = localizer;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private static string Normalize(string? value, int maxLength = MaxAnswerLength)
        {
            value = value?.Trim() ?? "";

            if (value.Length > maxLength)
            {
                value = value.Substring(0, maxLength);
            }

            return value;
        }

        private static string Encode(string? value)
        {
            return WebUtility.HtmlEncode(value ?? "");
        }

        private async Task<AppUser?> ResolveSurveyReceiverAsync(Submission? submission)
        {
            if (submission?.Conference?.TenantId != null)
            {
                var admins = await _userManager.GetUsersInRoleAsync("Admin");

                var tenantAdmin = admins.FirstOrDefault(x =>
                    x.TenantId.HasValue &&
                    x.TenantId.Value == submission.Conference.TenantId);

                if (tenantAdmin != null)
                {
                    return tenantAdmin;
                }
            }

            var superAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");

            var superAdmin = superAdmins.FirstOrDefault();

            if (superAdmin != null)
            {
                return superAdmin;
            }

            var allAdmins = await _userManager.GetUsersInRoleAsync("Admin");

            return allAdmins.FirstOrDefault()
                   ?? await _userManager.Users.FirstOrDefaultAsync();
        }

        [HttpGet("/Survey")]
        [HttpGet("/{slug}/Survey")]
        public async Task<IActionResult> Index(Guid? submissionId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            // Anket yalnızca bildiri sahibi yazarlar için geçerli; submissionId zorunlu
            if (!submissionId.HasValue || submissionId.Value == Guid.Empty)
            {
                TempData["ErrorMessage"] = T(
                    "SurveySubmissionRequired",
                    "Anket doldurabilmek için geçerli bir bildiri bağlantısı gereklidir.");

                return RedirectToAction("Index", "Dashboard");
            }

            var submission = await _context.Submissions
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Id == submissionId.Value &&
                    s.AuthorId == user.Id);

            if (submission == null)
            {
                TempData["ErrorMessage"] = T(
                    "UnauthorizedSubmissionSurvey",
                    "Bu bildiri için anket doldurma yetkiniz yok.");

                return RedirectToAction("Index", "Dashboard");
            }

            if (submission.IsFeedbackGiven)
            {
                TempData["InfoMessage"] = T(
                    "SurveyAlreadySubmitted",
                    "Bu bildiri için anket daha önce doldurulmuştur.");

                return RedirectToAction("Index", "Dashboard");
            }

            ViewBag.SubmissionId = submissionId;

            return View();
        }

        [HttpPost("/Survey/Submit")]
        [HttpPost("/{slug}/Survey/Submit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(
            Guid? submissionId,
            string? q1,
            string? q2,
            string? q3,
            string? q4,
            string? q5)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            q1 = Normalize(q1);
            q2 = Normalize(q2);
            q3 = Normalize(q3);
            q4 = Normalize(q4);
            q5 = Normalize(q5);

            if (string.IsNullOrWhiteSpace(q1) ||
                string.IsNullOrWhiteSpace(q2) ||
                string.IsNullOrWhiteSpace(q3) ||
                string.IsNullOrWhiteSpace(q4) ||
                string.IsNullOrWhiteSpace(q5))
            {
                TempData["ErrorMessage"] = T(
                    "FillRequiredFields",
                    "Lütfen tüm anket sorularını cevaplayın.");

                return RedirectToAction(nameof(Index), new { submissionId });
            }

            Submission? submission = null;

            if (submissionId.HasValue && submissionId.Value != Guid.Empty)
            {
                submission = await _context.Submissions
                    .Include(s => s.Conference)
                        .ThenInclude(c => c.Tenant)
                    .FirstOrDefaultAsync(s =>
                        s.Id == submissionId.Value &&
                        s.AuthorId == user.Id);

                if (submission == null)
                {
                    TempData["ErrorMessage"] = T(
                        "UnauthorizedSubmissionSurvey",
                        "Bu bildiri için anket gönderme yetkiniz yok.");

                    return RedirectToAction("Index", "Dashboard");
                }
            }

            var receiver = await ResolveSurveyReceiverAsync(submission);

            if (receiver == null)
            {
                TempData["ErrorMessage"] = T(
                    "AdminNotFound",
                    "Mesaj gönderilecek yönetici bulunamadı.");

                return RedirectToAction("Index", "Dashboard");
            }

            var fullName = $"{user.FirstName} {user.LastName}".Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = user.UserName ?? user.Email ?? T("UnknownUser", "Bilinmeyen Kullanıcı");
            }

            var submissionInfo = submission == null
                ? ""
                : $@"
                    <p>
                        <strong>{Encode(T("SubmissionLabel", "Bildiri"))}</strong>
                        {Encode(submission.Title)}
                    </p>";

            var surveyContent = $@"
                <p>
                    <strong>{Encode(T("UserLabel", "Kullanıcı:"))}</strong>
                    {Encode(fullName)} ({Encode(user.Email)})
                </p>

                {submissionInfo}

                <hr />

                <p>
                    <strong>{Encode(T("Question1Label", "Soru 1:"))}</strong><br />
                    {Encode(q1)}
                </p>

                <p>
                    <strong>{Encode(T("Question2Label", "Soru 2:"))}</strong><br />
                    {Encode(q2)}
                </p>

                <p>
                    <strong>{Encode(T("Question3Label", "Soru 3:"))}</strong><br />
                    {Encode(q3)}
                </p>

                <p>
                    <strong>{Encode(T("Question4Label", "Soru 4:"))}</strong><br />
                    {Encode(q4)}
                </p>

                <p>
                    <strong>{Encode(T("Question5Label", "Soru 5:"))}</strong><br />
                    {Encode(q5)}
                </p>
            ";

            var message = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = user.Id,
                ReceiverId = receiver.Id,
                Subject = T("SurveySubject", "Anket Geri Bildirimi"),
                Content = surveyContent,
                SentDate = DateTime.UtcNow,
                IsRead = false,
                IsDeleted = false
            };

            _context.Messages.Add(message);

            // Yapılandırılmış anket cevabını da kaydet (raporlanabilir)
            var surveyAnswer = new SurveyAnswer
            {
                UserId = user.Id,
                ConferenceId = submission?.ConferenceId ?? Guid.Empty,
                SubmissionId = submission?.Id,
                Answer1 = q1,
                Answer2 = q2,
                Answer3 = q3,
                Answer4 = q4,
                Answer5 = q5,
                SubmittedAt = DateTime.UtcNow
            };

            if (surveyAnswer.ConferenceId != Guid.Empty)
            {
                _context.SurveyAnswers.Add(surveyAnswer);
            }

            if (submission != null)
            {
                submission.IsFeedbackGiven = true;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "SurveySubmitSuccess",
                "Anketiniz başarıyla gönderildi.");

            if (submission != null)
            {
                return RedirectToAction(
                    "Details",
                    "Submission",
                    new { id = submission.Id });
            }

            return RedirectToAction("Index", "Dashboard");
        }
    }
}