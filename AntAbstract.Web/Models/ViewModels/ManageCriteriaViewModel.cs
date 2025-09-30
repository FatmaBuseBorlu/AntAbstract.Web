using AntAbstract.Domain.Entities;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AntAbstract.Web.Models.ViewModels
{
    public class ManageCriteriaViewModel
    {
        // Hangi formu yönettiğimizi bilmek için
        public ReviewForm Form { get; set; }

        // O forma ait mevcut kriterlerin listesi
        public List<ReviewCriterion> ExistingCriteria { get; set; }

        // Sayfadaki "Yeni Kriter Ekle" formu için kullanılacak olan nesne
        public ReviewCriterion NewCriterion { get; set; }

        // Kriter tiplerini (1-10 Puan, 1-5 Puan vb.) dropdown'da göstermek için
        public List<SelectListItem> CriterionTypes { get; set; }
    }
}