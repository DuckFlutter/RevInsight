using System.Reflection.PortableExecutable;

namespace DotNetRE.Core.Native;

public static class PeDetector
{
    public static PeDetectionResult Detect(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found.", filePath);
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new PEReader(stream, PEStreamOptions.LeaveOpen);
            var headers = reader.PEHeaders;
            if (headers is null || headers.PEHeader is null)
            {
                return new PeDetectionResult(false, false, "Not a PE file.");
            }

            var isDotNet = headers.CorHeader is not null;
            return new PeDetectionResult(true, isDotNet, isDotNet ? ".NET assembly" : "Native PE");
        }
        catch (BadImageFormatException)
        {
            return new PeDetectionResult(false, false, "Not a PE file.");
        }
    }
}

public sealed record PeDetectionResult(bool IsPe, bool IsDotNet, string Description);
