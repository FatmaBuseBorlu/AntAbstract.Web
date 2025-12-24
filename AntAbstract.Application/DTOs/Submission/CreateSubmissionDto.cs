using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Application.DTOs.Submission
{
    public class CreateSubmissionDto
    {
        public Guid ConferenceId { get; set; }

        [Required(ErrorMessage = "Başlık zorunludur.")]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required(ErrorMessage = "Özet metni zorunludur.")]
        public string Abstract { get; set; }

        public string Keywords { get; set; }
        public List<SubmissionAuthorDto> SubmissionAuthors { get; set; } = new();

        public string? FilePath { get; set; }
        public string? OriginalFileName { get; set; }
        public string? StoredFileName { get; set; }
    }
}