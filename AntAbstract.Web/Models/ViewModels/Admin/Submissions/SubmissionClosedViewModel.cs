using System;

namespace AntAbstract.Web.Models.ViewModels.Admin.Submissions
{
    /// <summary>
    /// Bildiri gönderimi kapalıyken gösterilen ekran. Kullanıcı eskiden kongre
    /// anasayfasına geri atılıyor ve sebebi orada bir uyarı şeridinde buluyordu;
    /// nerede olduğunu kaybediyordu. Artık bulunduğu sayfada kalıyor.
    /// </summary>
    public class SubmissionClosedViewModel
    {
        public string ConferenceTitle { get; set; } = string.Empty;

        /// <summary>Kapanma sebebini anlatan cümle.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Varsa geçmiş son tarih; ekranda ayrıca vurgulanıyor.</summary>
        public DateTime? Deadline { get; set; }

        /// <summary>Son tarih değil de ayarın kapalı olması durumunda true.</summary>
        public bool ClosedBySetting { get; set; }

        public string MySubmissionsUrl { get; set; } = string.Empty;

        public string ConferenceUrl { get; set; } = string.Empty;
    }
}
