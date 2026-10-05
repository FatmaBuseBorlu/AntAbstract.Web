using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Infrastructure.Services.Email
{
    /// <summary>
    /// Sistemin gönderdiği e-postaların varsayılan şablonları.
    ///
    /// Eskiden yalnızca "E-posta Şablonları" ekranı ilk açıldığında ve tablo
    /// tamamen boşken ekleniyordu: ekran hiç açılmadıysa kabul/ret ve sertifika
    /// e-postaları, listede hiç olmayan hatırlatmalar ise her zaman sessizce
    /// atlanıyordu (EmailService şablon bulamazsa göndermez). Artık uygulama
    /// açılışında eksik anahtarlar eklenir; yöneticinin düzenledikleri korunur.
    /// </summary>
    public static class EmailTemplateDefaults
    {
        public const string RegistrationReceived = "registration.received";
        public const string SubmissionReceived = "submission.received";
        public const string ConferenceUpdated = "conference.updated";
        public const string DeadlineReminder = "deadline_reminder";
        public const string PaymentPendingReminder = "payment_pending_reminder";

        /// <summary>Eksik şablonları ekler; mevcutlara dokunmaz. Eklenen sayısını döner.</summary>
        public static async Task<int> EnsureAsync(AppDbContext context, CancellationToken ct = default)
        {
            var existing = await context.EmailTemplates
                .Select(t => t.Key)
                .ToListAsync(ct);

            var missing = All()
                .Where(t => !existing.Contains(t.Key))
                .ToList();

            if (missing.Count == 0)
                return 0;

            context.EmailTemplates.AddRange(missing);
            await context.SaveChangesAsync(ct);
            return missing.Count;
        }

        public static IReadOnlyList<EmailTemplate> All() => new List<EmailTemplate>
        {
            Legacy("decision.accept", "Bildiri kabul edildiğinde yazara gönderilir.",
                "Bildiriniz Kabul Edildi – {ConferenceTitle}", "#16a34a", "✅ Bildiriniz Kabul Edildi",
                "<p><strong>{ConferenceTitle}</strong> kongresine gönderdiğiniz <em>{SubmissionTitle}</em> başlıklı bildiri kabul edilmiştir.</p>\n    {Note}"),

            Legacy("decision.reject", "Bildiri reddedildiğinde yazara gönderilir.",
                "Bildiri Değerlendirme Sonucu – {ConferenceTitle}", "#dc2626", "❌ Bildiri Değerlendirme Sonucu",
                "<p><strong>{ConferenceTitle}</strong> kongresine gönderdiğiniz <em>{SubmissionTitle}</em> başlıklı bildiri maalesef kabul edilememiştir.</p>\n    {Note}"),

            Legacy("decision.revision", "Bildiri revizyona gönderildiğinde yazara gönderilir.",
                "Bildiriniz Revizyon Gerektiriyor – {ConferenceTitle}", "#d97706", "🔄 Bildiriniz Revizyon Gerektiriyor",
                "<p><strong>{ConferenceTitle}</strong> kongresine gönderdiğiniz <em>{SubmissionTitle}</em> başlıklı bildirinizin revize edilmesi gerekmektedir.</p>\n    {Note}\n    <p>Lütfen sisteme giriş yaparak bildirinizi güncelleyiniz.</p>"),

            Legacy("certificate.author", "Yazar sertifikası oluşturulduğunda gönderilir.",
                "Katılım Sertifikanız Hazır – {ConferenceTitle}", "#7c3aed", "🏆 Sertifikanız Hazır",
                "<p><strong>{ConferenceTitle}</strong> kongresine yazar olarak katılımınıza ait sertifikanız hazırlanmıştır.</p>\n    " + Button("{DownloadLink}", "Sertifikayı İndir", "#7c3aed")),

            Legacy("certificate.reviewer", "Hakem sertifikası oluşturulduğunda gönderilir.",
                "Hakem Sertifikanız Hazır – {ConferenceTitle}", "#0284c7", "🏅 Hakem Sertifikanız Hazır",
                "<p><strong>{ConferenceTitle}</strong> kongresinde hakem olarak görev yaptığınız için teşekkür ederiz. Sertifikanız hazırlanmıştır.</p>\n    " + Button("{DownloadLink}", "Sertifikayı İndir", "#0284c7")),

            Legacy("certificate.attendee", "Dinleyici katılım sertifikası oluşturulduğunda gönderilir.",
                "Katılım Sertifikanız Hazır – {ConferenceTitle}", "#0891b2", "📜 Katılım Sertifikanız Hazır",
                "<p><strong>{ConferenceTitle}</strong> kongresine dinleyici olarak katılımınıza ait sertifikanız hazırlanmıştır.</p>\n    " + Button("{DownloadLink}", "Sertifikayı İndir", "#0891b2")),

            // ── Hatırlatmalar (MailReminderWorker) — önceden hiç eklenmiyordu ──
            Bilingual(DeadlineReminder,
                "Özet son tarihine 3 gün kala, kayıtlı olup henüz özet göndermeyenlere gönderilir. Yer tutucular: {FullName}, {ConferenceName}, {Deadline}, {DaysLeft}",
                "Özet gönderimi için son {DaysLeft} gün – {ConferenceName} / Abstract deadline reminder",
                "#d97706", "⏰ Özet son tarihi yaklaşıyor", "⏰ Abstract deadline approaching",
                "<p><strong>{ConferenceName}</strong> kongresine kaydınız var ancak henüz özet göndermediniz. Özet gönderimi <strong>{Deadline}</strong> tarihinde kapanıyor ({DaysLeft} gün kaldı).</p>",
                "<p>You are registered for <strong>{ConferenceName}</strong> but have not submitted an abstract yet. Submission closes on <strong>{Deadline}</strong> ({DaysLeft} days left).</p>"),

            Bilingual(PaymentPendingReminder,
                "Bekleyen ödemesi olan katılımcılara hatırlatma olarak gönderilir. Yer tutucular: {FullName}, {ConferenceName}, {Amount}, {PaymentDate}",
                "Bekleyen ödemeniz – {ConferenceName} / Pending payment",
                "#0284c7", "💳 Bekleyen ödemeniz var", "💳 You have a pending payment",
                "<p><strong>{ConferenceName}</strong> kongresi için <strong>{Amount}</strong> tutarındaki ödemeniz ({PaymentDate}) henüz tamamlanmadı. Sisteme giriş yaparak ödemenizi tamamlayabilirsiniz.</p>",
                "<p>Your payment of <strong>{Amount}</strong> for <strong>{ConferenceName}</strong> ({PaymentDate}) has not been completed yet. Please sign in to complete it.</p>"),

            // ── Yeni: kayıt, özet ve kongre değişikliği ──
            Bilingual(RegistrationReceived,
                "Katılımcı kongreye ön kayıt olduğunda gönderilir. Yer tutucular: {FullName}, {ConferenceTitle}, {RegistrationType}, {NextStepLink}, {ConferenceLink}",
                "Ön kaydınız alındı – {ConferenceTitle} / Pre-registration received",
                "#0ea5e9", "✅ Ön kaydınız alındı", "✅ Your pre-registration is received",
                "<p><strong>{ConferenceTitle}</strong> kongresine <strong>{RegistrationType}</strong> olarak ön kaydınız alınmıştır.</p>\n    <p>Bir sonraki adım: bildiri özetinizi göndermek. Ödeme, özetiniz kabul edildikten sonra istenecektir.</p>\n    " + Button("{NextStepLink}", "Devam Et", "#0ea5e9"),
                "<p>Your pre-registration for <strong>{ConferenceTitle}</strong> as <strong>{RegistrationType}</strong> has been received.</p>\n    <p>Next step: submit your abstract. Payment will be requested after your abstract is accepted.</p>\n    " + Button("{NextStepLink}", "Continue", "#0ea5e9")),

            Bilingual(SubmissionReceived,
                "Yazar bildiri özeti gönderdiğinde gönderilir. Yer tutucular: {FullName}, {ConferenceTitle}, {SubmissionTitle}, {SubmissionNo}, {SubmissionLink}",
                "Bildiri özetiniz alındı ({SubmissionNo}) – {ConferenceTitle} / Abstract received",
                "#16a34a", "📄 Bildiri özetiniz alındı", "📄 Your abstract is received",
                "<p><strong>{ConferenceTitle}</strong> kongresine gönderdiğiniz <em>{SubmissionTitle}</em> başlıklı bildiri özetiniz alınmıştır.</p>\n    <p>Bildiri numaranız: <strong>{SubmissionNo}</strong>. Değerlendirme sonucu size e-posta ile bildirilecektir.</p>\n    " + Button("{SubmissionLink}", "Bildirinizi Görüntüle", "#16a34a"),
                "<p>Your abstract <em>{SubmissionTitle}</em> for <strong>{ConferenceTitle}</strong> has been received.</p>\n    <p>Submission number: <strong>{SubmissionNo}</strong>. You will be notified of the review result by email.</p>\n    " + Button("{SubmissionLink}", "View Submission", "#16a34a")),

            Bilingual(ConferenceUpdated,
                "Yönetici kongre bilgilerini değiştirip 'katılımcılara bildir' seçtiğinde kayıtlılara gönderilir. Yer tutucular: {FullName}, {ConferenceTitle}, {Changes}, {ConferenceLink}",
                "Kongre bilgileri güncellendi – {ConferenceTitle} / Congress details updated",
                "#6366f1", "📢 Kongre bilgileri güncellendi", "📢 Congress details updated",
                "<p>Kayıtlı olduğunuz <strong>{ConferenceTitle}</strong> kongresinde şu bilgiler güncellenmiştir:</p>\n    {Changes}\n    " + Button("{ConferenceLink}", "Kongre Sayfası", "#6366f1"),
                "<p>The following details of <strong>{ConferenceTitle}</strong>, which you are registered for, have been updated:</p>\n    {Changes}\n    " + Button("{ConferenceLink}", "Congress Page", "#6366f1")),
        };

        private static string Button(string href, string text, string color) =>
            $"<p><a href='{href}' style='background:{color};color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;display:inline-block;margin-top:12px'>{text}</a></p>";

        // Eski altı şablon metni aynen korunur (yönetici düzenlemiş olabilir).
        private static EmailTemplate Legacy(string key, string description, string subject, string color, string heading, string body) => new()
        {
            Key = key,
            Description = description,
            Subject = subject,
            IsActive = true,
            HtmlBody = $@"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto'>
  <div style='background:{color};color:#fff;padding:24px 32px;border-radius:8px 8px 0 0'>
    <h2 style='margin:0'>{heading}</h2>
  </div>
  <div style='background:#f9fafb;padding:24px 32px;border-radius:0 0 8px 8px'>
    <p>Sayın <strong>{{FullName}}</strong>,</p>
    {body}
    <p style='margin-top:24px;color:#6b7280;font-size:13px'>Bu e-posta otomatik olarak gönderilmiştir.</p>
  </div>
</div>"
        };

        // Uluslararası katılımcılar için Türkçe + İngilizce tek e-postada.
        private static EmailTemplate Bilingual(string key, string description, string subject, string color,
            string headingTr, string headingEn, string bodyTr, string bodyEn) => new()
            {
                Key = key,
                Description = description,
                Subject = subject,
                IsActive = true,
                HtmlBody = $@"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto'>
  <div style='background:{color};color:#fff;padding:24px 32px;border-radius:8px 8px 0 0'>
    <h2 style='margin:0'>{headingTr}</h2>
  </div>
  <div style='background:#f9fafb;padding:24px 32px'>
    <p>Sayın <strong>{{FullName}}</strong>,</p>
    {bodyTr}
  </div>
  <div style='background:#ffffff;padding:24px 32px;border-top:1px solid #e5e7eb;border-radius:0 0 8px 8px;color:#374151'>
    <h3 style='margin:0 0 12px;font-size:16px'>{headingEn}</h3>
    <p>Dear <strong>{{FullName}}</strong>,</p>
    {bodyEn}
    <p style='margin-top:24px;color:#6b7280;font-size:13px'>Bu e-posta otomatik olarak gönderilmiştir. / This is an automated email.</p>
  </div>
</div>"
            };
    }
}
