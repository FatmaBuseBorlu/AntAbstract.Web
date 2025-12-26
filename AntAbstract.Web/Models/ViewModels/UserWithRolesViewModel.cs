using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class UserWithRolesViewModel
    {
        public string UserId { get; set; } = default!;
        public string? UserEmail { get; set; }
        public List<UserWithRoleViewModel> Roles { get; set; } = new();
    }

    public class UserWithRoleViewModel
    {
        public string RoleName { get; set; } = default!;
        public bool IsSelected { get; set; }
    }
}
