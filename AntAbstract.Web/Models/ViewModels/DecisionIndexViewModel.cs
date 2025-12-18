using System.Collections.Generic;
using AntAbstract.Domain.Entities;

namespace AntAbstract.Web.Models.ViewModels
{
    public class DecisionIndexViewModel
    {
        // Karar bekleyen bildiriler listesi
        public List<Submission> AwaitingDecision { get; set; }

        // Karar verilmiş bildiriler listesi
        public List<Submission> AlreadyDecided { get; set; }

        // Constructor (Yapıcı Metod) - Listelerin boş gelip hata vermemesi için
        public DecisionIndexViewModel()
        {
            AwaitingDecision = new List<Submission>();
            AlreadyDecided = new List<Submission>();
        }
    }
}