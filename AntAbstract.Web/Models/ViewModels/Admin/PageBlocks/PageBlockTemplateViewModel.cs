namespace AntAbstract.Web.Models.ViewModels.Admin.PageBlocks
{
    public class PageBlockTemplateViewModel
    {
        public int Order { get; set; }

        public string TechnicalType { get; set; } = string.Empty;

        public string NameTr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsDefault { get; set; }

        public bool IsActive { get; set; }
    }
}