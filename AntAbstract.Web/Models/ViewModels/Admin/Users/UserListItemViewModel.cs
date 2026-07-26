namespace AntAbstract.Web.Models.ViewModels.Admin.Users
{
    public class UserListItemViewModel
    {
        public string UserId { get; set; } = "";
        public string? Email { get; set; }
        public string? Name { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
        public bool IsLockedOut { get; set; }
        public bool EmailConfirmed { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string? LastLoginIp { get; set; }
        public int LoginCount { get; set; }
    }
}
