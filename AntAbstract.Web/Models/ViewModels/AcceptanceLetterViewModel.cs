namespace AntAbstract.Web.Models.ViewModels
{
    public class AcceptanceLetterViewModel
    {
        public string AuthorFullName { get; set; }
        public string AuthorInstitution { get; set; }
        public string SubmissionTitle { get; set; }
        public string ConferenceName { get; set; }
        public string ConferenceLogoPath { get; set; }
        public DateTime ConferenceStartDate { get; set; }
        public DateTime AcceptanceDate { get; set; }
        public string DocumentNumber { get; set; } = $"DA-{DateTime.Now.Year}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
    }
}
