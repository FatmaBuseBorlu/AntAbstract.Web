using System.Collections.Generic;
using AntAbstract.Domain.Entities;

namespace AntAbstract.Web.Models.ViewModels
{
    public class DecisionIndexViewModel
    {

        public List<Submission> AwaitingDecision { get; set; }

        public List<Submission> AlreadyDecided { get; set; }

        public DecisionIndexViewModel()
        {
            AwaitingDecision = new List<Submission>();
            AlreadyDecided = new List<Submission>();
        }
    }
}