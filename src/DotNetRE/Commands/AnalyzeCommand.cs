using Spectre.Console;
using Spectre.Console.Cli;
using DotNetRE.Core;
using DotNetRE.Core.Native;
using DotNetRE.Core.Formats;

namespace DotNetRE.Commands;

public sealed class AnalyzeSettings : AssemblyCommandSettings
{
}

public sealed class AnalyzeCommand : AsyncCommand<AnalyzeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeSettings settings)
    {
        var updateChecker = new UpdateChecker();
        if (settings.CheckUpdate)
        {
            var currentVersion = typeof(AnalyzeCommand).Assembly.GetName().Version ?? new Version(0, 1, 0);
            await updateChecker.CheckAndDisplayAsync(currentVersion);
        }

        var typeResult = FileTypeDetector.Detect(settings.AssemblyPath);

        if (typeResult.Format == FileFormat.DotNet)
        {
            var analyzer = new AssemblyAnalyzer();
            var result = AnsiConsole.Status()
                .Start("Analyzing .NET assembly...", _ => analyzer.Analyze(settings.AssemblyPath));
            RenderManagedSummary(result);
            return 0;
        }

        if (typeResult.Format == FileFormat.NativePe)
        {
            var nativeAnalyzer = new NativePeAnalyzer();
            var nativeResult = AnsiConsole.Status()
                .Start("Analyzing native PE...", _ => nativeAnalyzer.Analyze(settings.AssemblyPath));

            RenderNativeSummary(nativeResult);
            return 0;
        }

        var formatAnalyzer = new FormatAnalyzer();
        var formatResult = AnsiConsole.Status()
            .Start($"Analyzing {typeResult.Description}...", _ => formatAnalyzer.Analyze(settings.AssemblyPath, typeResult));

        RenderFormatSummary(formatResult);

        return 0;
    }

    private static void RenderManagedSummary(AnalysisResult result)
    {
        var metaTable = new Table().RoundedBorder().AddColumn("Property").AddColumn("Value");
        metaTable.AddRow("Name", result.Metadata.Name);
        metaTable.AddRow("Runtime", result.Metadata.RuntimeVersion);
        metaTable.AddRow("Types", result.Metadata.TypeCount.ToString());
        metaTable.AddRow("Methods", result.Metadata.MethodCount.ToString());
        metaTable.AddRow("Size", $"{result.Metadata.FileSize:N0} bytes");
        metaTable.AddRow("Entropy", result.Metadata.Entropy.ToString("F3"));
        AnsiConsole.Write(metaTable);

        var findingsTable = new Table().RoundedBorder().AddColumn("Status").AddColumn("Finding").AddColumn("Details");
        if (result.Findings.Count == 0)
        {
            findingsTable.AddRow("🟢", "Clean", "No obfuscation markers detected.");
        }
        else
        {
            foreach (var finding in result.Findings)
            {
                var status = finding.Severity switch
                {
                    FindingSeverity.Warning => "🟡",
                    FindingSeverity.Obfuscated => "🔴",
                    _ => "🟢"
                };
                findingsTable.AddRow(status, finding.Title, finding.Details);
            }
        }

        AnsiConsole.Write(findingsTable);
    }

    private static void RenderNativeSummary(NativeAnalysisResult result)
    {
        var metaTable = new Table().RoundedBorder().AddColumn("Property").AddColumn("Value");
        metaTable.AddRow("File", result.FileName);
        metaTable.AddRow("Architecture", result.Architecture);
        metaTable.AddRow("Entry Point", result.EntryPoint);
        metaTable.AddRow("Size", $"{result.FileSize:N0} bytes");
        AnsiConsole.Write(metaTable);

        var sectionTable = new Table().RoundedBorder().AddColumn("Section").AddColumn("VirtSize").AddColumn("RawSize").AddColumn("Entropy");
        foreach (var section in result.Sections)
        {
            sectionTable.AddRow(section.Name, section.VirtualSize.ToString(), section.RawSize.ToString(), section.Entropy.ToString("F3"));
        }
        AnsiConsole.Write(sectionTable);

        var importTable = new Table().RoundedBorder().AddColumn("Imports");
        foreach (var import in result.Imports.Take(50))
        {
            importTable.AddRow(import);
        }
        if (result.Imports.Count > 50)
        {
            importTable.AddRow($"... {result.Imports.Count - 50} more");
        }
        AnsiConsole.Write(importTable);

        var exportTable = new Table().RoundedBorder().AddColumn("Exports");
        if (result.Exports.Count == 0)
        {
            exportTable.AddRow("(none)");
        }
        else
        {
            foreach (var export in result.Exports.Take(50))
            {
                exportTable.AddRow(export);
            }
            if (result.Exports.Count > 50)
            {
                exportTable.AddRow($"... {result.Exports.Count - 50} more");
            }
        }
        AnsiConsole.Write(exportTable);

        var resTable = new Table().RoundedBorder().AddColumn("Resources").AddColumn("Count");
        if (result.Resources.Count == 0)
        {
            resTable.AddRow("(none)", "0");
        }
        else
        {
            foreach (var res in result.Resources)
            {
                resTable.AddRow(res.Type, res.Count.ToString());
            }
        }
        AnsiConsole.Write(resTable);

        var hintsTable = new Table().RoundedBorder().AddColumn("Compiler Hints").AddColumn("Packer Hints");
        var compiler = result.CompilerHints.Count == 0 ? "(none)" : string.Join("; ", result.CompilerHints);
        var packer = result.PackerHints.Count == 0 ? "(none)" : string.Join("; ", result.PackerHints);
        hintsTable.AddRow(compiler, packer);
        AnsiConsole.Write(hintsTable);

        var stringTable = new Table().RoundedBorder().AddColumn("Strings (sample)");
        foreach (var str in result.Strings.Take(40))
        {
            stringTable.AddRow(str);
        }
        if (result.Strings.Count > 40)
        {
            stringTable.AddRow($"... {result.Strings.Count - 40} more");
        }
        AnsiConsole.Write(stringTable);

        var disTable = new Table().RoundedBorder().AddColumn("Entry Point Disassembly");
        foreach (var line in result.Disassembly)
        {
            disTable.AddRow(line);
        }
        AnsiConsole.Write(disTable);
    }

    private static void RenderFormatSummary(FormatAnalysisResult result)
    {
        var metaTable = new Table().RoundedBorder().AddColumn("Property").AddColumn("Value");
        metaTable.AddRow("File", result.FileName);
        metaTable.AddRow("Format", result.Format.ToString());
        metaTable.AddRow("Size", $"{result.FileSize:N0} bytes");
        foreach (var pair in result.Metadata)
        {
            metaTable.AddRow(pair.Key, pair.Value);
        }
        AnsiConsole.Write(metaTable);

        if (result.Notes.Count > 0)
        {
            var notesTable = new Table().RoundedBorder().AddColumn("Notes");
            foreach (var note in result.Notes)
            {
                notesTable.AddRow(note);
            }
            AnsiConsole.Write(notesTable);
        }

        if (result.Entries.Count > 0)
        {
            var entryTable = new Table().RoundedBorder().AddColumn("Entries");
            foreach (var entry in result.Entries)
            {
                entryTable.AddRow(entry);
            }
            AnsiConsole.Write(entryTable);
        }

        if (result.Strings.Count > 0)
        {
            var stringTable = new Table().RoundedBorder().AddColumn("Strings (sample)");
            foreach (var str in result.Strings.Take(40))
            {
                stringTable.AddRow(str);
            }
            AnsiConsole.Write(stringTable);
        }
    }
}
