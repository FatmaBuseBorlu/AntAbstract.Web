using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class ManageUserRolesViewModel
    {
        public string UserId { get; set; }
        public string UserEmail { get; set; }

        public List<UserRoleViewModel> Roles { get; set; }
    }

    public class UserRoleViewModel
    {
        public string RoleName { get; set; }
        public bool IsSelected { get; set; } 
    }
}