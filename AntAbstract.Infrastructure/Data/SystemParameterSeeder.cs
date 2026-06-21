using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;

namespace AntAbstract.Infrastructure.Data
{
    public static class SystemParameterSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            if (await context.SystemParameters.AnyAsync())
                return;

            var json = ReadEmbeddedJson();
            if (!json.HasValue) return;

            var parameters = new List<SystemParameter>();

            foreach (var group in json.Value.EnumerateObject())
            {
                int order = 0;
                foreach (var item in group.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        parameters.Add(new SystemParameter
                        {
                            Group = group.Name,
                            Name = item.GetString()!,
                            Order = order++
                        });
                    }
                    else if (item.ValueKind == JsonValueKind.Object)
                    {
                        parameters.Add(new SystemParameter
                        {
                            Group = group.Name,
                            Name = item.GetProperty("name").GetString()!,
                            NameEn = item.TryGetProperty("nameEn", out var en) ? en.GetString() : null,
                            Order = order++
                        });
                    }
                }
            }

            context.SystemParameters.AddRange(parameters);
            await context.SaveChangesAsync();
        }

        private static JsonElement? ReadEmbeddedJson()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var filePath = Path.Combine(dir, "SeedData", "system-parameters.json");

            if (!File.Exists(filePath))
            {
                var projectDir = FindProjectRoot(dir);
                if (projectDir != null)
                    filePath = Path.Combine(projectDir, "Data", "SeedData", "system-parameters.json");
            }

            if (!File.Exists(filePath))
                return null;

            var text = File.ReadAllText(filePath);
            return JsonDocument.Parse(text).RootElement;
        }

        private static string? FindProjectRoot(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "AntAbstract.Infrastructure.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
