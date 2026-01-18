using Octokit;
using Spectre.Console;

namespace DotNetRE.Core;

public sealed class UpdateChecker
{
    private const string Owner = "DuckFlutter";
    private const string Repo = "DotNetRE";

    public async Task<Release?> GetLatestReleaseAsync()
    {
        var client = new GitHubClient(new ProductHeaderValue("DotNetRE"));
        try
        {
            return await client.Repository.Release.GetLatest(Owner, Repo);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> CheckAndDisplayAsync(Version currentVersion)
    {
        var latest = await GetLatestReleaseAsync();
        if (latest is null)
        {
            AnsiConsole.MarkupLine("[yellow]Update check failed or no releases found.[/]");
            return false;
        }

        if (!Version.TryParse(latest.TagName.TrimStart('v', 'V'), out var latestVersion))
        {
            AnsiConsole.MarkupLine("[yellow]Latest release tag is not a valid version.[/]");
            return false;
        }

        if (latestVersion <= currentVersion)
        {
            AnsiConsole.MarkupLine($"[green]Up to date:[/] v{currentVersion}");
            return false;
        }

        AnsiConsole.MarkupLine($"[yellow]Update available:[/] v{currentVersion} → v{latestVersion}");
        return true;
    }

    public async Task<string?> DownloadLatestAsync(string outputDirectory, Func<ReleaseAsset, bool>? selector = null)
    {
        var latest = await GetLatestReleaseAsync();
        if (latest is null)
        {
            return null;
        }

        var asset = latest.Assets.FirstOrDefault(selector ?? DefaultSelector);
        if (asset is null)
        {
            return null;
        }

        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, asset.Name);

        using var http = new HttpClient();
        using var stream = await http.GetStreamAsync(asset.BrowserDownloadUrl);
        using var file = File.Create(outputPath);
        await stream.CopyToAsync(file);

        return outputPath;
    }

    private static bool DefaultSelector(ReleaseAsset asset)
    {
        var name = asset.Name.ToLowerInvariant();
        return name.EndsWith(".exe") || name.Contains("linux") || name.EndsWith(".zip");
    }
}
