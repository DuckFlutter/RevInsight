using System.IO.Compression;
using dnlib.DotNet;

namespace DotNetRE.Core.Unpackers;

public sealed class StaticUnpacker
{
    public UnpackResult TryUnpackConfuserEx(string assemblyPath, string outputDirectory)
    {
        var module = ModuleDefMD.Load(assemblyPath);
        var embedded = module.Resources
            .OfType<EmbeddedResource>()
            .FirstOrDefault(r => r.Name.IndexOf("payload", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 r.Name.IndexOf("confuser", StringComparison.OrdinalIgnoreCase) >= 0);

        if (embedded is null)
        {
            return new UnpackResult(false, "No ConfuserEx payload resource detected.", null);
        }

        var data = embedded.CreateReader().ReadBytes((int)embedded.Length);
        var unpacked = TryDecompress(data) ?? data;

        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "unpacked.bin");
        File.WriteAllBytes(outputPath, unpacked);

        return new UnpackResult(true, "Extracted embedded payload resource.", outputPath);
    }

    private static byte[]? TryDecompress(byte[] data)
    {
        if (data.Length < 2 || data[0] != 0x1F || data[1] != 0x8B)
        {
            return null;
        }

        try
        {
            using var input = new MemoryStream(data);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            return null;
        }
    }
}

public sealed record UnpackResult(bool Success, string Message, string? OutputPath);
