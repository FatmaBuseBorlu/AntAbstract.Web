using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Ödeme gönderimi üretimde 500 veriyordu: PaymentController sınıfında
/// [Route("{slug}/Payment")] varken eylemde hem [HttpPost("Process")] hem
/// [HttpPost("/{slug}/payment/process")] tanımlıydı. İkisi birleşince aynı
/// adresi üretiyor, istek iki uca birden eşleşiyor ve ASP.NET
/// AmbiguousMatchException fırlatıyordu. Aynı çakışma Success ve Cancel
/// eylemlerinde de vardı; yani ödemenin tamamlanma yolu bütünüyle kapalıydı.
///
/// Rotalar büyük/küçük harfe duyarsız olduğu için gözle fark edilmesi zor.
/// Bu test tüm denetleyicileri tarar.
/// </summary>
public sealed class RouteAmbiguityTests
{
    private readonly ITestOutputHelper _output;

    public RouteAmbiguityTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void NoController_DefinesTwoRoutesForTheSameUrl()
    {
        var controllers = typeof(Program).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(controllers);

        var findings = new List<string>();

        foreach (var controller in controllers)
        {
            var classRoute = controller
                .GetCustomAttributes<RouteAttribute>()
                .Select(a => a.Template)
                .FirstOrDefault();

            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var attributes = action
                    .GetCustomAttributes()
                    .OfType<IRouteTemplateProvider>()
                    .Where(a => a is IActionHttpMethodProvider)
                    .ToList();

                if (attributes.Count < 2)
                {
                    continue;
                }

                var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var attribute in attributes)
                {
                    var template = attribute.Template;

                    if (string.IsNullOrWhiteSpace(template))
                    {
                        continue;
                    }

                    var methods = string.Join(
                        ",",
                        ((IActionHttpMethodProvider)attribute).HttpMethods.OrderBy(x => x));

                    var full = template.StartsWith('/')
                        ? template
                        : $"/{classRoute}/{template}";

                    var key = $"{methods} {full.Replace("//", "/").TrimEnd('/')}";

                    if (seen.TryGetValue(key, out var previous))
                    {
                        findings.Add(
                            $"{controller.Name}.{action.Name}: \"{previous}\" ve \"{template}\" aynı adrese çıkıyor ({key})");
                    }
                    else
                    {
                        seen[key] = template;
                    }
                }
            }
        }

        foreach (var finding in findings)
        {
            _output.WriteLine(finding);
        }

        Assert.True(
            findings.Count == 0,
            $"{findings.Count} eylemde aynı adresi üreten iki rota var; istek iki uca birden eşleşir ve 500 döner.");
    }
}
