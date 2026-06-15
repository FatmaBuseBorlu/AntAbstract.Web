using System;

namespace AntAbstract.Web.Models.ViewModels.Admin.Submissions
{
    public class SubmissionEditViewModel : SubmissionCreateViewModel
    {
        public Guid Id { get; set; }

        public string ExistingFilePath { get; set; } = string.Empty;
        public int ExistingFileId { get; set; }
    }
}