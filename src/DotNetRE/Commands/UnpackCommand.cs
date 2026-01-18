using Spectre.Console;
using Spectre.Console.Cli;
using DotNetRE.Core;
using DotNetRE.Core.Unpackers;
using DotNetRE.Core.Output;
using DotNetRE.Core.Native;
using DotNetRE.Core.Formats;

namespace DotNetRE.Commands;

public sealed class UnpackSettings : UnpackCommandSettings
{
}

public sealed class UnpackCommand : AsyncCommand<UnpackSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, UnpackSettings settings)
    {
        var updateChecker = new UpdateChecker();
        if (settings.CheckUpdate)
        {
            var currentVersion = typeof(UnpackCommand).Assembly.GetName().Version ?? new Version(0, 1, 0);
            await updateChecker.CheckAndDisplayAsync(currentVersion);
        }

        var analyzer = new AssemblyAnalyzer();
        var detection = FileTypeDetector.Detect(settings.AssemblyPath);
        if (detection.Format != FileFormat.DotNet)
        {
            AnsiConsole.MarkupLine("[red]Unpacking is only supported for .NET assemblies.[/]");
            return 1;
        }

        var analysis = analyzer.Analyze(settings.AssemblyPath);
        if (analysis.ObfuscationDetected && !settings.Auto)
        {
            var proceed = AnsiConsole.Confirm("Obfuscation detected. Proceed with unpacking?", false);
            if (!proceed)
            {
                AnsiConsole.MarkupLine("[yellow]Unpacking cancelled by user.[/]");
                return 0;
            }
        }

        var outputProvider = new OutputPathProvider();
        var runDir = outputProvider.CreateRunDirectory(settings.OutputDirectory);
        var unpackDir = Path.Combine(runDir, "unpack");
        Directory.CreateDirectory(unpackDir);

        var unpacker = new StaticUnpacker();
        var result = AnsiConsole.Status()
            .Start("Attempting static unpack...", _ => unpacker.TryUnpackConfuserEx(settings.AssemblyPath, unpackDir));

        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]{result.Message}[/]");
            if (result.OutputPath is not null)
            {
                AnsiConsole.MarkupLine($"[green]Output:[/] {result.OutputPath}");
            }
            return 0;
        }

        AnsiConsole.MarkupLine($"[yellow]{result.Message}[/]");
        return 1;
    }
}
