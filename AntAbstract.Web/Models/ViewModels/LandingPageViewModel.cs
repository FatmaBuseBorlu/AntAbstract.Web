using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class LandingPageViewModel
    {
        public int TotalUsers { get; set; }
        public int ActiveCongressesCount { get; set; }
        public List<CongressCardDto> ActiveCongresses { get; set; } = new();

        public List<SubmissionCardDto> LastSubmissions { get; set; } = new();
    }

    public class CongressCardDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime StartDate { get; set; }
        public string Location { get; set; }
        public string ImageUrl { get; set; }
        public string Slug { get; set; }
        public bool IsRegistered { get; set; }
    }

    public class SubmissionCardDto
    {
        public string Title { get; set; }        
        public string AbstractSnippet { get; set; } 
        public string AuthorName { get; set; }    
        public string University { get; set; }   
        public string ConferenceName { get; set; } 
        public string AuthorImageUrl { get; set; } 
    }
}