namespace AntAbstract.Application.DTOs.Review
{
    public class SubmitReviewDto
    {
        public int ReviewAssignmentId { get; set; } 
        public string CommentsToAuthor { get; set; } 
        public string Recommendation { get; set; } 
        public int Score { get; set; } 
    }
}