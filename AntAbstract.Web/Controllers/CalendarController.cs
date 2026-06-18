using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    /// <summary>
    /// Konferans etkinliklerini ICS (iCalendar) formatında dışa aktarır.
    /// Kullanıcı "Takvime Ekle" butonuna tıkladığında bu endpoint çağrılır.
    /// </summary>
    public class CalendarController : Controller
    {
        private readonly AppDbContext _context;

        public CalendarController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("/calendar/conference/{conferenceId:guid}.ics")]
        [HttpGet("/{slug}/calendar/conference/{conferenceId:guid}.ics")]
        public async Task<IActionResult> ConferenceIcs(Guid conferenceId, string? slug = null)
        {
            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null) return NotFound();

            var sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//AntAbstract//Conference//TR");
            sb.AppendLine("CALSCALE:GREGORIAN");
            sb.AppendLine("METHOD:PUBLISH");

            // Ana kongre etkinliği
            {
                var start = conference.StartDate;
                var end = conference.EndDate.AddDays(1);

                sb.AppendLine("BEGIN:VEVENT");
                sb.AppendLine($"UID:{conference.Id}@antabstract");
                sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
                sb.AppendLine($"DTSTART;VALUE=DATE:{start:yyyyMMdd}");
                sb.AppendLine($"DTEND;VALUE=DATE:{end:yyyyMMdd}");
                sb.AppendLine($"SUMMARY:{EscapeIcs(conference.Title ?? "Kongre")}");
                sb.AppendLine($"DESCRIPTION:{EscapeIcs(conference.Description ?? "")}");
                sb.AppendLine("END:VEVENT");
            }

            // Son bildiri gönderim tarihi
            if (conference.AbstractSubmissionDeadline.HasValue)
            {
                var dl = conference.AbstractSubmissionDeadline.Value;
                sb.AppendLine("BEGIN:VEVENT");
                sb.AppendLine($"UID:{conference.Id}-submission-deadline@antabstract");
                sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
                sb.AppendLine($"DTSTART;VALUE=DATE:{dl:yyyyMMdd}");
                sb.AppendLine($"DTEND;VALUE=DATE:{dl.AddDays(1):yyyyMMdd}");
                sb.AppendLine($"SUMMARY:Son Özet Başvuru — {EscapeIcs(conference.Title ?? "Kongre")}");
                sb.AppendLine("END:VEVENT");
            }

            sb.AppendLine("END:VCALENDAR");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var safeName = (conference.Slug ?? conference.Id.ToString("N")).Replace(" ", "-");
            return File(bytes, "text/calendar; charset=utf-8", $"{safeName}.ics");
        }

        private static string EscapeIcs(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace(";", "\\;")
                .Replace(",", "\\,")
                .Replace("\r\n", "\\n")
                .Replace("\n", "\\n");
        }
    }
}
