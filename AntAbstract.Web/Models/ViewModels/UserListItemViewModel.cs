namespace AntAbstract.Web.Models.ViewModels
{
    public class UserListItemViewModel
    {
        public string UserId { get; set; } = "";
        public string? Email { get; set; }
        public string? Name { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
    }
}
