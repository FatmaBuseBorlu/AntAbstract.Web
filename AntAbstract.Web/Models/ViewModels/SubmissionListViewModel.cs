using AntAbstract.Domain.Entities;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    // Bu sınıf, Controller'dan View'a veri taşır
    public class SubmissionListViewModel
    {
        // HATA VEREN VE ARANAN PROPERY BUDUR!
        // IEnumerable kullanmak, listeyi okumak için daha genel bir tiptir.
        public IEnumerable<Submission> Submissions { get; set; } = new List<Submission>();
    }
}