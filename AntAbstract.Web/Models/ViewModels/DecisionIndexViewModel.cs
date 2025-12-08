using AntAbstract.Domain.Entities;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class DecisionIndexViewModel
    {
        public List<Submission> AwaitingDecision { get; set; }
        public List<Submission> AlreadyDecided { get; set; }
    }
}