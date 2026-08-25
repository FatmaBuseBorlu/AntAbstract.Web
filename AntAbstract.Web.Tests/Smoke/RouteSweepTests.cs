using System.Net;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests.Smoke;

/// <summary>
/// Public ve kongre bazlı rotaların hiçbirinin 500 dönmediğini doğrular.
/// Test çıktısında her rotanın durum kodu raporlanır; bir sayfa bozulduğunda
/// hangisi olduğu doğrudan görünür.
/// </summary>
public sealed class RouteSweepTests(ITestOutputHelper output)
{
    [Fact]
    public async Task PublicRoutes_DoNotReturn500()
    {
        using var factory = new RegistrationAvailabilityFactory();
        factory.SeedConference("sweep-kongre");

        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        string[] routes =
        [
            "/",
            "/congresses",
            "/Home/Congresses",
            "/Home/About",
            "/Home/Contact",
            "/Home/Privacy",
            "/Home/Terms",
            "/Home/Proceedings",
            "/Identity/Account/Login",
            "/Identity/Account/Register",
            "/sweep-kongre",
            "/sweep-kongre/registration",
            "/sweep-kongre/register",
            "/sweep-kongre/program",
            "/sweep-kongre/submit-abstract",
            "/sweep-kongre/my-submissions",
            "/sweep-kongre/payments",
            "/sweep-kongre/Dashboard",
            "/Dashboard/MyConferences",
            "/yok-boyle-bir-slug",
            "/yok-boyle-bir-slug/registration"
        ];

        var failures = new List<string>();

        foreach (var route in routes)
        {
            HttpResponseMessage response;

            try
            {
                response = await client.GetAsync(route);
            }
            catch (Exception ex)
            {
                output.WriteLine($"{route,-40} EXCEPTION {ex.GetType().Name}: {ex.Message}");
                failures.Add(route);
                continue;
            }

            var location = response.Headers.Location?.ToString() ?? "";

            output.WriteLine($"{route,-40} {(int)response.StatusCode} {response.StatusCode} {location}");

            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                failures.Add(route);
            }
        }

        Assert.Empty(failures);
    }
}
