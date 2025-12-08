using AntAbstract.Domain.Entities;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AntAbstract.Web.Models.ViewModels
{
    public class SessionDetailViewModel
    {
        public Session Session { get; set; }

        public SelectList AvailableSubmissions { get; set; }

        public Guid SubmissionIdToAdd { get; set; }
    }
}