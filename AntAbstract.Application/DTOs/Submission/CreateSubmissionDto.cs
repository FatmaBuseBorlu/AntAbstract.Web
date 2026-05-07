using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Application.DTOs.Submission
{
    public class CreateSubmissionDto
    {
        public Guid ConferenceId { get; set; }

        public Guid? ConferenceTopicId { get; set; }

        [Required(ErrorMessage = "Başlık zorunludur.")]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Özet metni zorunludur.")]
        public string Abstract { get; set; } = string.Empty;

        public string Keywords { get; set; } = string.Empty;

        public string Topic { get; set; } = string.Empty;

        public string PresentationType { get; set; } = string.Empty;

        public string? FilePath { get; set; }

        public string? OriginalFileName { get; set; }

        public string? StoredFileName { get; set; }

        public List<SubmissionAuthorDto> SubmissionAuthors { get; set; } = new();
    }
}