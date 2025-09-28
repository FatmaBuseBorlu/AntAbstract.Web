using AntAbstract.Domain.Entities;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class DecisionIndexViewModel
    {
        // Karar bekleyen özetlerin listesi
        public List<Submission> AwaitingDecision { get; set; }

        // Kararı zaten verilmiş olan özetlerin listesi
        public List<Submission> AlreadyDecided { get; set; }
    }
}