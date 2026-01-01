using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels.Admin.Users
{
    public class ManageUserRolesViewModel
    {
        public string UserId { get; set; } = default!;
        public string? UserEmail { get; set; }
        public List<UserRoleViewModel> Roles { get; set; } = new();
    }

    public class UserRoleViewModel
    {
        public string RoleName { get; set; } = default!;
        public bool IsSelected { get; set; }
    }
}
