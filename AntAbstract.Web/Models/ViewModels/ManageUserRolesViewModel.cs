using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class ManageUserRolesViewModel
    {
        public string UserId { get; set; }
        public string UserEmail { get; set; }

        // Sistdeki tüm rolleri ve kullanıcının o role sahip olup olmadığını tutan liste
        public List<UserRoleViewModel> Roles { get; set; }
    }

    // Tek bir rolü temsil eden yardımcı sınıf
    public class UserRoleViewModel
    {
        public string RoleName { get; set; }
        public bool IsSelected { get; set; } // Kullanıcı bu role sahip mi?
    }
}