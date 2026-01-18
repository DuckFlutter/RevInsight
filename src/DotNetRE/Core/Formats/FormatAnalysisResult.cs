namespace DotNetRE.Core.Formats;

public sealed record FormatAnalysisResult(
    FileFormat Format,
    string FileName,
    long FileSize,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> Strings,
    IReadOnlyList<string> Entries
);
