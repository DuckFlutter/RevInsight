using System.Globalization;

namespace DotNetRE.Core.Output;

public sealed class OutputPathProvider
{
    public string CreateRunDirectory(string? baseDirectory)
    {
        var root = string.IsNullOrWhiteSpace(baseDirectory)
            ? Path.Combine(Environment.CurrentDirectory, "output")
            : Path.GetFullPath(baseDirectory);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var runDir = Path.Combine(root, timestamp);
        Directory.CreateDirectory(runDir);
        return runDir;
    }
}
