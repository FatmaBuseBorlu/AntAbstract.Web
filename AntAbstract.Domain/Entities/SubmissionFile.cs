using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Domain.Entities
{
    public class SubmissionFile
    {
        public int Id { get; set; }

        public string FileName { get; set; } 
        public string StoredFileName { get; set; } 
        public string FilePath { get; set; } 

        public SubmissionFileType Type { get; set; } 
        public int Version { get; set; }
        public DateTime UploadedAt { get; set; }

        public Guid SubmissionId { get; set; }

        [ForeignKey("SubmissionId")]
        public Submission Submission { get; set; }
    }
}