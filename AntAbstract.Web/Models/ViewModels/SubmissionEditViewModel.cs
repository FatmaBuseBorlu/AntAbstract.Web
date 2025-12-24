using System;

namespace AntAbstract.Web.Models.ViewModels
{
    public class SubmissionEditViewModel : SubmissionCreateViewModel
    {
        public Guid Id { get; set; }

        public string ExistingFilePath { get; set; }
    }
}