namespace DotNetRE.Core.Native;

public sealed record NativeAnalysisResult(
    string FileName,
    string Architecture,
    string EntryPoint,
    long FileSize,
    IReadOnlyList<SectionInfo> Sections,
    IReadOnlyList<string> Imports,
    IReadOnlyList<string> Exports,
    IReadOnlyList<ResourceInfo> Resources,
    IReadOnlyList<string> Strings,
    IReadOnlyList<string> Disassembly,
    IReadOnlyList<string> CompilerHints,
    IReadOnlyList<string> PackerHints
);

public sealed record SectionInfo(string Name, uint VirtualSize, uint RawSize, double Entropy);

public sealed record ResourceInfo(string Type, int Count);
