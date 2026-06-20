namespace AntAbstract.Infrastructure.Services.Doi
{
    public sealed class DoiOptions
    {
        public const string SectionName = "Doi";

        public string Provider { get; set; } = "Manual";

        public string Prefix { get; set; } = "";

        public string LandingPageBaseUrl { get; set; } = "";
    }
}
