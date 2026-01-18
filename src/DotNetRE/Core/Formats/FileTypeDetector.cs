using System.Buffers.Binary;
using DotNetRE.Core.Native;

namespace DotNetRE.Core.Formats;

public enum FileFormat
{
    DotNet,
    NativePe,
    Elf,
    MachO,
    MachOFat,
    Wasm,
    JavaClass,
    Jar,
    Apk,
    Dex,
    Asar,
    Script,
    Unknown
}

public sealed record FileTypeResult(FileFormat Format, string Description);

public static class FileTypeDetector
{
    public static FileTypeResult Detect(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found.", filePath);
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (IsScriptExtension(ext))
        {
            return new FileTypeResult(FileFormat.Script, "Script file");
        }

        var fileInfo = new FileInfo(filePath);
        var length = (int)Math.Min(4096, fileInfo.Length);
        var buffer = new byte[length];
        using (var stream = File.OpenRead(filePath))
        {
            stream.ReadExactly(buffer, 0, length);
        }
        if (buffer.Length >= 2 && buffer[0] == 0x4D && buffer[1] == 0x5A)
        {
            var pe = PeDetector.Detect(filePath);
            return new FileTypeResult(pe.IsDotNet ? FileFormat.DotNet : FileFormat.NativePe, pe.Description);
        }

        if (buffer.Length >= 4 && buffer[0] == 0x7F && buffer[1] == (byte)'E' && buffer[2] == (byte)'L' && buffer[3] == (byte)'F')
        {
            return new FileTypeResult(FileFormat.Elf, "ELF binary");
        }

        if (buffer.Length >= 4)
        {
            var magic = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan());
            if (magic is 0xFEEDFACE or 0xFEEDFACF or 0xCEFAEDFE or 0xCFFAEDFE)
            {
                return new FileTypeResult(FileFormat.MachO, "Mach-O binary");
            }

            if (magic is 0xCAFEBABE or 0xBEBAFECA)
            {
                if (ext == ".class")
                {
                    return new FileTypeResult(FileFormat.JavaClass, "Java class file");
                }

                return new FileTypeResult(FileFormat.MachOFat, "Mach-O fat binary");
            }
        }

        if (buffer.Length >= 4)
        {
            var classMagic = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan());
            if (classMagic == 0xCAFEBABE)
            {
                return new FileTypeResult(FileFormat.JavaClass, "Java class file");
            }
        }

        if (buffer.Length >= 4 && buffer[0] == 0x00 && buffer[1] == 0x61 && buffer[2] == 0x73 && buffer[3] == 0x6D)
        {
            return new FileTypeResult(FileFormat.Wasm, "WebAssembly module");
        }

        if (buffer.Length >= 4 && buffer[0] == 0x50 && buffer[1] == 0x4B)
        {
            if (ext == ".apk")
            {
                return new FileTypeResult(FileFormat.Apk, "Android APK (ZIP container)");
            }

            if (ext == ".jar")
            {
                return new FileTypeResult(FileFormat.Jar, "Java JAR (ZIP container)");
            }

            if (ext == ".asar")
            {
                return new FileTypeResult(FileFormat.Asar, "Electron ASAR container");
            }

            return new FileTypeResult(FileFormat.Jar, "ZIP container (treat as JAR)");
        }

        if (buffer.Length >= 8 && System.Text.Encoding.ASCII.GetString(buffer[..8]).StartsWith("dex\n"))
        {
            return new FileTypeResult(FileFormat.Dex, "Android DEX bytecode");
        }

        if (ext == ".asar")
        {
            return new FileTypeResult(FileFormat.Asar, "Electron ASAR container");
        }

        return new FileTypeResult(FileFormat.Unknown, "Unknown format");
    }

    private static bool IsScriptExtension(string ext)
    {
        return ext is ".ps1" or ".psm1" or ".vbs" or ".js" or ".jse" or ".wsf" or ".hta" or ".au3" or ".bat" or ".cmd";
    }
}
