using AntAbstract.Domain.Entities;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class SubmissionListViewModel
    {

        public IEnumerable<Submission> Submissions { get; set; } = new List<Submission>();
    }
}