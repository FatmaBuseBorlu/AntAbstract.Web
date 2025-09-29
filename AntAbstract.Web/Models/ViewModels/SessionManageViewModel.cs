using AntAbstract.Domain.Entities;
using System.Collections.Generic;

// Sizin mevcut yapınıza uygun namespace
namespace AntAbstract.Web.Models.ViewModels
{
    public class SessionManageViewModel
    {
        // Yönetilmekte olan oturumun kendisi
        public Session Session { get; set; }

        // Bu oturuma DAHA ÖNCEDEN atanmış olan özetler
        public List<Submission> AssignedSubmissions { get; set; }

        // Bu oturuma ATANABİLECEK durumda olan özetler 
        // (Kabul edilmiş ama başka bir oturuma atanmamış)
        public List<Submission> AvailableSubmissions { get; set; }
    }
}