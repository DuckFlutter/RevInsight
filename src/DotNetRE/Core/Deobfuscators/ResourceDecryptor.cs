using System.IO.Compression;
using dnlib.DotNet;

namespace DotNetRE.Core.Deobfuscators;

public sealed class ResourceDecryptor : IDeobfuscator
{
    public string Name => "Resource Decryptor";

    public DeobfuscationResult Apply(ModuleDefMD module)
    {
        var changes = 0;
        for (var i = 0; i < module.Resources.Count; i++)
        {
            if (module.Resources[i] is not EmbeddedResource embedded)
            {
                continue;
            }

            var data = embedded.CreateReader().ReadBytes((int)embedded.Length);
            if (data.Length < 2 || data[0] != 0x1F || data[1] != 0x8B)
            {
                continue;
            }

            using var input = new MemoryStream(data);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            try
            {
                gzip.CopyTo(output);
            }
            catch
            {
                continue;
            }

            var decompressed = output.ToArray();
            module.Resources[i] = new EmbeddedResource(embedded.Name, decompressed, embedded.Attributes);
            changes++;
        }

        var notes = changes == 0
            ? "No compressed resources decompressed."
            : "Decompressed gzip-encoded embedded resources.";
        return new DeobfuscationResult(Name, changes, notes);
    }
}
