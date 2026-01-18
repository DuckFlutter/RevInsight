using Spectre.Console;
using Spectre.Console.Cli;
using DotNetRE.Core;
using DotNetRE.Core.Output;

namespace DotNetRE.Commands;

public sealed class UpdateSettings : CommandSettings
{
    [CommandOption("--output <DIR>")]
    public string? OutputDirectory { get; init; }
}

public sealed class UpdateCommand : AsyncCommand<UpdateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, UpdateSettings settings)
    {
        var checker = new UpdateChecker();
        var currentVersion = typeof(UpdateCommand).Assembly.GetName().Version ?? new Version(0, 1, 0);
        var hasUpdate = await checker.CheckAndDisplayAsync(currentVersion);
        if (!hasUpdate)
        {
            return 0;
        }

        var outputProvider = new OutputPathProvider();
        var runDir = outputProvider.CreateRunDirectory(settings.OutputDirectory);
        var downloaded = await checker.DownloadLatestAsync(runDir);

        if (downloaded is null)
        {
            AnsiConsole.MarkupLine("[yellow]No suitable release asset found to download.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Downloaded update to:[/] {downloaded}");
        return 0;
    }
}
