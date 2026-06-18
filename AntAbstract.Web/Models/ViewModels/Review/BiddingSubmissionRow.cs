using System;

namespace AntAbstract.Web.Models.ViewModels.Review
{
    public class BiddingSubmissionRow
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Abstract { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
    }
}
