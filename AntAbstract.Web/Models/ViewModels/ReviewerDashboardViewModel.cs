using AntAbstract.Domain.Entities;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class ReviewerDashboardViewModel
    {
        // Hakeme atanmış toplam özet sayısı
        public int TotalAssigned { get; set; }

        // Hakemin değerlendirmeyi tamamladığı özet sayısı
        public int CompletedReviews { get; set; }

        // Hakemin henüz değerlendirmediği, bekleyen özet sayısı
        public int PendingReviews { get; set; }

        // Panelde hızlıca listelemek için, değerlendirme bekleyen görevler
        public List<ReviewAssignment> PendingAssignments { get; set; }

        public ReviewerDashboardViewModel()
        {
            // Listeyi null olmaktan kurtarmak için boş olarak başlatıyoruz.
            PendingAssignments = new List<ReviewAssignment>();
        }
    }
}