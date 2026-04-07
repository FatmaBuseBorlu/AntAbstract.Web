using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class RegistrationType
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required(ErrorMessage = "Kayıt türü adı zorunludur.")]
        [StringLength(100, ErrorMessage = "Kayıt türü adı en fazla 100 karakter olabilir.")]
        [Display(Name = "Kayıt Türü Adı")]
        public string Name { get; set; }

        [StringLength(500)]
        [Display(Name = "Açıklama")]
        public string Description { get; set; } = "";

        [Required(ErrorMessage = "Fiyat zorunludur.")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 999999.99)]
        [Display(Name = "Fiyat")]
        public decimal Price { get; set; }

        [Required]
        [StringLength(10)]
        [Display(Name = "Para Birimi")]
        public string Currency { get; set; } = "TRY"; 

        [Display(Name = "Son Satış Tarihi")]
        [DataType(DataType.DateTime)]
        public DateTime? Deadline { get; set; } 

        [Display(Name = "Aktif mi?")]
        public bool IsActive { get; set; } = true; 

        [Required]
        [Display(Name = "Kongre")]
        public Guid ConferenceId { get; set; }

        [ForeignKey("ConferenceId")]
        public virtual Conference Conference { get; set; }
    }
}