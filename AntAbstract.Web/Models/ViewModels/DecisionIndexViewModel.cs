using System.Collections.Generic;
using AntAbstract.Domain.Entities;

namespace AntAbstract.Web.Models.ViewModels
{
    public class DecisionIndexViewModel
    {
        public List<Submission> AwaitingDecision { get; set; } = new List<Submission>();
        public List<Submission> AlreadyDecided { get; set; } = new List<Submission>();
    }
}