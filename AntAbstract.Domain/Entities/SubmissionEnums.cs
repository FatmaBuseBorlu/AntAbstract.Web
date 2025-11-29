using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Domain.Entities
{
    public enum SubmissionStatus
    {
        New = 0,
        UnderReview = 1,
        Accepted = 2,
        Rejected = 3,
        RevisionRequired = 4,
        Presented = 5,
        Withdrawn = 6
    }

    public enum SubmissionFileType
    {
        FullTextDoc = 0,
        AbstractPdf = 1,
        Presentation = 2,
        Receipt = 3
    }
}