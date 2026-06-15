namespace AntAbstract.Application.DTOs.Review
{
    public class SubmitReviewDto
    {
        public int ReviewAssignmentId { get; set; } 
        public string CommentsToAuthor { get; set; } 
        public string Recommendation { get; set; } 
        public int Score { get; set; }
        public int ScoreOriginality { get; set; }
        public int ScoreMethodology { get; set; }
        public int ScorePresentation { get; set; }
        public int ScoreRelevance { get; set; }
    }
}