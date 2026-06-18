using AntAbstract.Domain.Entities;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels.Admin.Assignment
{
    public class AssignReviewerViewModel
    {
        public Submission Submission { get; set; } = null!;
        public List<AppUser> RecommendedReviewers { get; set; } = new();
        public List<AppUser> AllOtherReviewers { get; set; } = new();
    }
}
