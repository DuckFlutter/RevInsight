using Spectre.Console.Cli;
using DotNetRE.Core.Output;

namespace DotNetRE.Commands;

public abstract class AssemblyCommandSettings : CommandSettings
{
    [CommandArgument(0, "<ASSEMBLY>")]
    public string AssemblyPath { get; init; } = string.Empty;

    [CommandOption("--check-update")]
    public bool CheckUpdate { get; init; }
}

public abstract class DeobfuscationCommandSettings : AssemblyCommandSettings
{
    [CommandOption("--format <FORMAT>")]
    public OutputFormat Format { get; init; } = OutputFormat.Cs;

    [CommandOption("--auto")]
    public bool Auto { get; init; }

    [CommandOption("--output <DIR>")]
    public string? OutputDirectory { get; init; }
}

public abstract class UnpackCommandSettings : AssemblyCommandSettings
{
    [CommandOption("--auto")]
    public bool Auto { get; init; }

    [CommandOption("--output <DIR>")]
    public string? OutputDirectory { get; init; }
}
