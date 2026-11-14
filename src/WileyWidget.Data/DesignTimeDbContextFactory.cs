#nullable enable

using System;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WileyWidget.Data;

/// <summary>
/// EF design-time factory. Prefer env / API Development.local (NAS Synology Postgres);
/// do not fall back to a Mac Docker Postgres placeholder.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrWhiteSpace(connectionString)
            && !string.Equals(
                Environment.GetEnvironmentVariable("WILEY_EF_DESIGN_SKIP_LOCAL_SETTINGS"),
                "1",
                StringComparison.Ordinal))
        {
            connectionString = TryLoadFromApiLocalSettings();
        }

        if (string.IsNullOrWhiteSpace(connectionString)
            || connectionString.Contains("USE_DEVELOPMENT_LOCAL_OR_USER_SECRETS", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Design-time DefaultConnection is not configured. Set ConnectionStrings__DefaultConnection " +
                "(or DATABASE_URL), or create WileyCoWeb.Api/appsettings.Development.local.json pointing at " +
                "NAS Postgres (Host=mr-storage;Port=5433;Database=wileyco_dev;Username=wiley_app).");
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(Environment.ExpandEnvironmentVariables(connectionString), npgsql =>
        {
            npgsql.CommandTimeout(30);
        });

        return new AppDbContext(optionsBuilder.Options);
    }

    private static string? TryLoadFromApiLocalSettings()
    {
        foreach (var apiDir in CandidateApiDirs())
        {
            var local = Path.Combine(apiDir, "appsettings.Development.local.json");
            if (!File.Exists(local))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(local));
                if (doc.RootElement.TryGetProperty("ConnectionStrings", out var css)
                    && css.TryGetProperty("DefaultConnection", out var cs)
                    && cs.ValueKind == JsonValueKind.String)
                {
                    var value = cs.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
            catch (JsonException)
            {
                // try next candidate
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateApiDirs()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var full = Path.GetFullPath(path);
            if (Directory.Exists(full) && seen.Add(full))
            {
                list.Add(full);
            }
        }

        Add(Path.Combine(Directory.GetCurrentDirectory(), "WileyCoWeb.Api"));
        Add(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "WileyCoWeb.Api")));

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            Add(Path.Combine(dir.FullName, "WileyCoWeb.Api"));
        }

        return list;
    }
}
