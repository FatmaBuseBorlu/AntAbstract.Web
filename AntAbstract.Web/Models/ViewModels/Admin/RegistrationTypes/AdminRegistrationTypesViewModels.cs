using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Web.Models.ViewModels.Admin.RegistrationTypes
{
    public class AdminRegistrationTypeRowModel
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = "";

        public string? NameEn { get; set; }

        public string? Description { get; set; }

        public string? DescriptionEn { get; set; }

        public decimal Price { get; set; }

        public string Currency { get; set; } = "TRY";

        public int UsageCount { get; set; }
    }

    public class AdminRegistrationTypesIndexModel
    {
        public string Slug { get; set; } = "";

        public Guid ConferenceId { get; set; }

        public string ConferenceTitle { get; set; } = "";

        public List<AdminRegistrationTypeRowModel> Items { get; set; } = new();
    }

    public class AdminRegistrationTypeFormModel
    {
        public string Slug { get; set; } = "";

        public Guid ConferenceId { get; set; }

        public string ConferenceTitle { get; set; } = "";

        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Kayıt türü adı zorunludur.")]
        [StringLength(100, ErrorMessage = "Kayıt türü adı en fazla 100 karakter olabilir.")]
        public string Name { get; set; } = "";

        [StringLength(100, ErrorMessage = "İngilizce kayıt türü adı en fazla 100 karakter olabilir.")]
        public string? NameEn { get; set; }

        [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
        public string? Description { get; set; }

        [StringLength(500, ErrorMessage = "İngilizce açıklama en fazla 500 karakter olabilir.")]
        public string? DescriptionEn { get; set; }

        [Range(0, 999999999, ErrorMessage = "Fiyat 0 ile 999999999 arasında olmalıdır.")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Para birimi zorunludur.")]
        [StringLength(10, ErrorMessage = "Para birimi en fazla 10 karakter olabilir.")]
        public string Currency { get; set; } = "TRY";

        public string ReturnUrl { get; set; } = "";
    }
}