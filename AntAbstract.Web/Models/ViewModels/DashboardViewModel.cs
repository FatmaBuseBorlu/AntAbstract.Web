using System.Collections.Generic; // List için eklendi

namespace AntAbstract.Web.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalSubmissions { get; set; }
        public int TotalReviews { get; set; }

        //  GRAFİK İÇİN YENİ EKLENEN ALANLAR
        public List<string> ChartLabels { get; set; }
        public List<int> ChartData { get; set; }

        public int AcceptedSubmissions { get; set; }
        public int AwaitingDecision { get; set; }

        public DashboardViewModel()
        {
            // Listelerin null olmasını engellemek için boş olarak başlatıyoruz.
            ChartLabels = new List<string>();
            ChartData = new List<int>();
        }
    }
}