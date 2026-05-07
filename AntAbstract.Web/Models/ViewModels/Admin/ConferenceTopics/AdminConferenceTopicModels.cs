using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Web.Models.ViewModels.Admin.ConferenceTopics
{
    public class AdminConferenceTopicRowModel
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = "";

        public string? NameEn { get; set; }

        public string? Description { get; set; }

        public string? DescriptionEn { get; set; }

        public bool IsActive { get; set; }

        public int SortOrder { get; set; }

        public int SubmissionCount { get; set; }
    }

    public class AdminConferenceTopicsIndexModel
    {
        public string Slug { get; set; } = "";

        public Guid ConferenceId { get; set; }

        public string ConferenceTitle { get; set; } = "";

        public List<AdminConferenceTopicRowModel> Items { get; set; } = new();
    }

    public class AdminConferenceTopicFormModel
    {
        public Guid? Id { get; set; }

        public string Slug { get; set; } = "";

        public Guid ConferenceId { get; set; }

        public string ConferenceTitle { get; set; } = "";

        public string ReturnUrl { get; set; } = "";

        [Required(ErrorMessage = "Konu adı zorunludur.")]
        [StringLength(150, ErrorMessage = "Konu adı en fazla 150 karakter olabilir.")]
        public string Name { get; set; } = "";

        [StringLength(150, ErrorMessage = "İngilizce konu adı en fazla 150 karakter olabilir.")]
        public string? NameEn { get; set; }

        [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
        public string? Description { get; set; }

        [StringLength(500, ErrorMessage = "İngilizce açıklama en fazla 500 karakter olabilir.")]
        public string? DescriptionEn { get; set; }

        public bool IsActive { get; set; } = true;

        [Range(0, 9999, ErrorMessage = "Sıralama değeri 0 ile 9999 arasında olmalıdır.")]
        public int SortOrder { get; set; } = 0;
    }
}