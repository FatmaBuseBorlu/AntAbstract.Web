using AntAbstract.Infrastructure.Services.Doi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AntAbstract.Infrastructure.Tests.Services;

public sealed class DataCiteDoiTests
{
    [Fact]
    public void IsConfigured_ReturnsFalse_WhenPlaceholders()
    {
        var svc = CreateService(new Dictionary<string, string?>
        {
            ["Doi:RepositoryId"] = "#{DOI_REPOSITORY_ID}#",
            ["Doi:Password"] = "#{DOI_PASSWORD}#",
            ["Doi:Prefix"] = "10.12345",
            ["Doi:LandingPageBaseUrl"] = "https://antabstract.com.tr"
        });

        Assert.False(svc.IsConfigured);
    }

    [Fact]
    public void IsConfigured_ReturnsFalse_WhenPrefixMissing()
    {
        var svc = CreateService(new Dictionary<string, string?>
        {
            ["Doi:RepositoryId"] = "REPO.ID",
            ["Doi:Password"] = "secret",
            ["Doi:Prefix"] = "",
            ["Doi:LandingPageBaseUrl"] = "https://antabstract.com.tr"
        });

        Assert.False(svc.IsConfigured);
    }

    [Fact]
    public void IsConfigured_ReturnsFalse_WhenLandingPagePlaceholder()
    {
        var svc = CreateService(new Dictionary<string, string?>
        {
            ["Doi:RepositoryId"] = "REPO.ID",
            ["Doi:Password"] = "secret",
            ["Doi:Prefix"] = "10.12345",
            ["Doi:LandingPageBaseUrl"] = "#{PUBLIC_BASE_URL}#"
        });

        Assert.False(svc.IsConfigured);
    }

    [Fact]
    public void IsConfigured_ReturnsTrue_WhenAllSet()
    {
        var svc = CreateService(new Dictionary<string, string?>
        {
            ["Doi:RepositoryId"] = "REPO.ID",
            ["Doi:Password"] = "secret",
            ["Doi:Prefix"] = "10.12345",
            ["Doi:LandingPageBaseUrl"] = "https://antabstract.com.tr"
        });

        Assert.True(svc.IsConfigured);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsError_WhenNotConfigured()
    {
        var svc = CreateService(new Dictionary<string, string?>());

        var result = await svc.RegisterAsync(new DoiRegistrationRequest
        {
            SubmissionId = Guid.NewGuid(),
            Title = "Test"
        });

        Assert.False(result.Success);
        Assert.Contains("yapılandırılmamış", result.Error);
    }

    private static DataCiteDoiService CreateService(Dictionary<string, string?> config)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        var httpFactory = new TestHttpClientFactory();
        return new DataCiteDoiService(configuration, httpFactory, NullLogger<DataCiteDoiService>.Instance);
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
