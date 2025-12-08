using AntAbstract.Domain.Entities;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class SessionManageViewModel
    {
    
        public Session Session { get; set; }
        public List<Submission> AssignedSubmissions { get; set; }

        public List<Submission> AvailableSubmissions { get; set; }
    }
}