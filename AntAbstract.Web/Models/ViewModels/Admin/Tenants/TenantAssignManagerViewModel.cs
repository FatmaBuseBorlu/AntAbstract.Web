using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Web.Models.ViewModels.Admin.Tenants
{
    public class TenantAssignManagerViewModel
    {
        public Guid TenantId { get; set; }

        public string? TenantName { get; set; }

        public string AssignmentMode { get; set; } = "Existing";

        public string? ExistingUserId { get; set; }

        public List<SelectListItem> AvailableUsers { get; set; } = new();

        [Display(Name = "Ad")]
        [StringLength(50, ErrorMessage = "Ad en fazla 50 karakter olabilir.")]
        public string? FirstName { get; set; }

        [Display(Name = "Soyad")]
        [StringLength(50, ErrorMessage = "Soyad en fazla 50 karakter olabilir.")]
        public string? LastName { get; set; }

        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [Display(Name = "E-posta")]
        public string? Email { get; set; }

        [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
        [Display(Name = "Şifre")]
        public string? Password { get; set; }
        public string? ReturnUrl { get; set; }
    }
}