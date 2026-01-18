using dnlib.DotNet;
using Spectre.Console;
using Spectre.Console.Cli;
using DotNetRE.Core;
using DotNetRE.Core.Output;
using DotNetRE.Core.Native;
using DotNetRE.Core.Formats;

namespace DotNetRE.Commands;

public sealed class DeobfuscateSettings : DeobfuscationCommandSettings
{
}

public sealed class DeobfuscateCommand : AsyncCommand<DeobfuscateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DeobfuscateSettings settings)
    {
        var updateChecker = new UpdateChecker();
        if (settings.CheckUpdate)
        {
            var currentVersion = typeof(DeobfuscateCommand).Assembly.GetName().Version ?? new Version(0, 1, 0);
            await updateChecker.CheckAndDisplayAsync(currentVersion);
        }

        var analyzer = new AssemblyAnalyzer();
        var detection = FileTypeDetector.Detect(settings.AssemblyPath);
        if (detection.Format != FileFormat.DotNet)
        {
            AnsiConsole.MarkupLine("[red]Deobfuscation is only supported for .NET assemblies.[/]");
            return 1;
        }

        var analysis = analyzer.Analyze(settings.AssemblyPath);

        if (analysis.ObfuscationDetected && !settings.Auto)
        {
            var proceed = AnsiConsole.Confirm("Obfuscation detected. Proceed with deobfuscation?", false);
            if (!proceed)
            {
                AnsiConsole.MarkupLine("[yellow]Deobfuscation cancelled by user.[/]");
                return 0;
            }
        }

        var outputProvider = new OutputPathProvider();
        var runDir = outputProvider.CreateRunDirectory(settings.OutputDirectory);
        var assemblyDir = Path.Combine(runDir, "assembly");
        var sourceDir = Path.Combine(runDir, "source");
        Directory.CreateDirectory(assemblyDir);
        Directory.CreateDirectory(sourceDir);

        var progress = new Table().RoundedBorder().AddColumn("Stage").AddColumn("Status");
        progress.AddRow("Load assembly", "⏳");
        progress.AddRow("Run anti-anti", "⏳");
        progress.AddRow("Run deobfuscators", "⏳");
        progress.AddRow("Write assembly", "⏳");
        progress.AddRow("Generate source", "⏳");

        ModuleDefMD? module = null;
        IReadOnlyList<DotNetRE.Core.AntiAnti.AntiAntiResult>? antiAntiResults = null;
        IReadOnlyList<DotNetRE.Core.Deobfuscators.DeobfuscationResult>? deobfuscationResults = null;
        string? outputAssembly = null;

        AnsiConsole.Live(progress).Start(ctx =>
        {
            progress.UpdateCell(0, 1, "▶️");
            ctx.Refresh();
            module = ModuleDefMD.Load(settings.AssemblyPath);
            progress.UpdateCell(0, 1, "✅");

            progress.UpdateCell(1, 1, "▶️");
            ctx.Refresh();
            var engine = new DeobfuscationEngine();
            antiAntiResults = engine.RunAntiAnti(module);
            progress.UpdateCell(1, 1, "✅");

            progress.UpdateCell(2, 1, "▶️");
            ctx.Refresh();
            deobfuscationResults = engine.RunDeobfuscators(module);
            progress.UpdateCell(2, 1, "✅");

            progress.UpdateCell(3, 1, "▶️");
            ctx.Refresh();
            var outputName = Path.GetFileNameWithoutExtension(settings.AssemblyPath) + "-deobfuscated" + Path.GetExtension(settings.AssemblyPath);
            outputAssembly = Path.Combine(assemblyDir, outputName);
            module.Write(outputAssembly);
            progress.UpdateCell(3, 1, "✅");

            progress.UpdateCell(4, 1, "▶️");
            ctx.Refresh();
            var generator = new SourceGenerator();
            generator.Generate(outputAssembly, sourceDir, settings.Format);
            progress.UpdateCell(4, 1, "✅");
            ctx.Refresh();
        });

        if (antiAntiResults is not null && deobfuscationResults is not null && outputAssembly is not null)
        {
            RenderReport(new DeobfuscationReport(antiAntiResults, deobfuscationResults), outputAssembly, sourceDir);
        }
        return 0;
    }

    private static void RenderReport(DeobfuscationReport report, string outputAssembly, string sourceDir)
    {
        AnsiConsole.MarkupLine($"[green]Deobfuscated assembly:[/] {outputAssembly}");
        AnsiConsole.MarkupLine($"[green]Source output:[/] {sourceDir}");

        var antiTable = new Table().RoundedBorder().AddColumn("Module").AddColumn("Changes").AddColumn("Notes");
        foreach (var item in report.AntiAntiResults)
        {
            antiTable.AddRow(item.Name, item.Changes.ToString(), item.Notes);
        }
        AnsiConsole.Write(antiTable);

        var deobTable = new Table().RoundedBorder().AddColumn("Module").AddColumn("Changes").AddColumn("Notes");
        foreach (var item in report.DeobfuscationResults)
        {
            deobTable.AddRow(item.Name, item.Changes.ToString(), item.Notes);
        }
        AnsiConsole.Write(deobTable);
    }
}
