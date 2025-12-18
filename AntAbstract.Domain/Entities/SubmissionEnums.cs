using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Domain.Entities
{
    public enum SubmissionStatus
    {
        // --- ESKİ KODLARIN ARADIĞI ---
        New = 0,
        Presented = 1,
        Withdrawn = 2,

        // --- YENİ SİSTEMİN ARADIĞI ---
        Pending = 10,           // Karar Bekliyor
        UnderReview = 11,       // Hakemde
        Accepted = 12,          // Kabul
        Rejected = 13,          // Red
        RevisionRequired = 14   // Revizyon
    }

    public enum SubmissionFileType
    {
        // Yeni İsimler
        FullText = 1,
        Abstract = 2,
        Presentation = 3,
        Supplementary = 4,

        // --- ESKİ KODLARIN ARADIĞI (Eşleştirme) ---
        FullTextDoc = 1 // Eski kod "FullTextDoc" dediğinde "FullText" (1) anlasın.
    }
}