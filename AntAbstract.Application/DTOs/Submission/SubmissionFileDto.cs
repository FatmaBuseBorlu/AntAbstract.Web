using System;

namespace AntAbstract.Application.DTOs.Submission
{
    public class SubmissionFileDto
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Type { get; set; } 
        public int Version { get; set; }
        public DateTime UploadedAt { get; set; }
        public Guid SubmissionId { get; set; }
    }
}