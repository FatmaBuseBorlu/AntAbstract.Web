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

        public string FileName { get; set; } // Kullanıcının yüklediği isim
        public string StoredFileName { get; set; } // Sunucudaki güvenli isim (Guid)
        public string FilePath { get; set; } // Klasör yolu

        public SubmissionFileType Type { get; set; } // Enum
        public int Version { get; set; }
        public DateTime UploadedAt { get; set; }

        // İlişki
        public Guid SubmissionId { get; set; }

        [ForeignKey("SubmissionId")]
        public Submission Submission { get; set; }
    }
}