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
        Presented = 1,
        Withdrawn = 2,


        Pending = 10,           
        UnderReview = 11,       
        Accepted = 12,         
        Rejected = 13,          
        RevisionRequired = 14   
    }

    public enum SubmissionFileType
    {
        FullText = 1,
        Abstract = 2,
        Presentation = 3,
        Supplementary = 4,

        FullTextDoc = 1 
    }
}