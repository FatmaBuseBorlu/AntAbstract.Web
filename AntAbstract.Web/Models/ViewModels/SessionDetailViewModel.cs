using AntAbstract.Domain.Entities;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AntAbstract.Web.Models.ViewModels
{
    public class SessionDetailViewModel
    {
        public Session Session { get; set; }

        // Bu oturuma HENÜZ ATANMAMIŞ ama "Kabul Edildi" durumundaki özetleri
        // dropdown'da göstermek için kullanılacak liste.
        public SelectList AvailableSubmissions { get; set; }

        // Dropdown'dan seçilen özetin ID'sini tutmak için.
        public Guid SubmissionIdToAdd { get; set; }
    }
}