using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace DotNetRE.Core.Formats;

public sealed class FormatAnalyzer
{
    public FormatAnalysisResult Analyze(string filePath, FileTypeResult typeResult)
    {
        return typeResult.Format switch
        {
            FileFormat.Elf => AnalyzeElf(filePath),
            FileFormat.MachO => AnalyzeMachO(filePath),
            FileFormat.MachOFat => AnalyzeMachOFat(filePath),
            FileFormat.Wasm => AnalyzeWasm(filePath),
            FileFormat.JavaClass => AnalyzeJavaClass(filePath),
            FileFormat.Jar => AnalyzeZipContainer(filePath, FileFormat.Jar),
            FileFormat.Apk => AnalyzeZipContainer(filePath, FileFormat.Apk),
            FileFormat.Dex => AnalyzeDex(filePath),
            FileFormat.Asar => AnalyzeAsar(filePath),
            FileFormat.Script => AnalyzeScript(filePath),
            _ => AnalyzeUnknown(filePath, typeResult)
        };
    }

    private static FormatAnalysisResult AnalyzeElf(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var metadata = new Dictionary<string, string>();
        if (bytes.Length < 0x20)
        {
            return BasicResult(FileFormat.Elf, filePath, bytes.LongLength, metadata, new[] { "ELF header too small." });
        }

        var is64 = bytes[4] == 2;
        var endian = bytes[5] == 2 ? "Big" : "Little";
        var osAbi = bytes[7];
        metadata["Class"] = is64 ? "ELF64" : "ELF32";
        metadata["Endian"] = endian;
        metadata["OSABI"] = osAbi.ToString();

        var read16 = (int offset) => ReadUInt16(bytes, offset, endian == "Big");
        var read32 = (int offset) => ReadUInt32(bytes, offset, endian == "Big");
        var read64 = (int offset) => ReadUInt64(bytes, offset, endian == "Big");

        var eType = read16(16);
        var eMachine = read16(18);
        var entry = is64 ? read64(24) : read32(24);
        var phnum = read16(is64 ? 56 : 44);
        var shnum = read16(is64 ? 60 : 48);

        metadata["Type"] = $"0x{eType:X}";
        metadata["Machine"] = $"0x{eMachine:X}";
        metadata["EntryPoint"] = $"0x{entry:X}";
        metadata["ProgramHeaders"] = phnum.ToString();
        metadata["SectionHeaders"] = shnum.ToString();

        return BasicResult(FileFormat.Elf, filePath, bytes.LongLength, metadata);
    }

    private static FormatAnalysisResult AnalyzeMachO(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var metadata = new Dictionary<string, string>();
        if (bytes.Length < 28)
        {
            return BasicResult(FileFormat.MachO, filePath, bytes.LongLength, metadata, new[] { "Mach-O header too small." });
        }

        var magic = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        var is64 = magic is 0xFEEDFACF or 0xCFFAEDFE;
        var isBig = magic is 0xFEEDFACE or 0xFEEDFACF;

        var read32 = (int offset) => ReadUInt32(bytes, offset, isBig);
        metadata["Magic"] = $"0x{magic:X}";
        metadata["Class"] = is64 ? "Mach-O 64" : "Mach-O 32";
        metadata["CPUType"] = $"0x{read32(4):X}";
        metadata["CPUSubType"] = $"0x{read32(8):X}";
        metadata["FileType"] = $"0x{read32(12):X}";
        metadata["Commands"] = read32(16).ToString();

        return BasicResult(FileFormat.MachO, filePath, bytes.LongLength, metadata);
    }

    private static FormatAnalysisResult AnalyzeMachOFat(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var metadata = new Dictionary<string, string>();
        if (bytes.Length < 8)
        {
            return BasicResult(FileFormat.MachOFat, filePath, bytes.LongLength, metadata, new[] { "Fat header too small." });
        }

        var magic = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        var isBig = magic == 0xCAFEBABE;
        var nfat = ReadUInt32(bytes, 4, isBig);
        metadata["Magic"] = $"0x{magic:X}";
        metadata["Architectures"] = nfat.ToString();
        return BasicResult(FileFormat.MachOFat, filePath, bytes.LongLength, metadata);
    }

    private static FormatAnalysisResult AnalyzeWasm(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var metadata = new Dictionary<string, string>();
        if (bytes.Length >= 8)
        {
            var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4));
            metadata["Version"] = version.ToString();
        }

        return BasicResult(FileFormat.Wasm, filePath, bytes.LongLength, metadata);
    }

    private static FormatAnalysisResult AnalyzeJavaClass(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var metadata = new Dictionary<string, string>();
        if (bytes.Length >= 10)
        {
            var minor = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(4, 2));
            var major = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(6, 2));
            var cpCount = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(8, 2));
            metadata["Version"] = $"{major}.{minor}";
            metadata["ConstantPoolCount"] = cpCount.ToString();
        }

        return BasicResult(FileFormat.JavaClass, filePath, bytes.LongLength, metadata);
    }

    private static FormatAnalysisResult AnalyzeDex(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var metadata = new Dictionary<string, string>();
        if (bytes.Length >= 8)
        {
            var magic = Encoding.ASCII.GetString(bytes.AsSpan(0, 8));
            metadata["Magic"] = magic.TrimEnd('\0');
        }

        return BasicResult(FileFormat.Dex, filePath, bytes.LongLength, metadata);
    }

    private static FormatAnalysisResult AnalyzeZipContainer(string filePath, FileFormat format)
    {
        var metadata = new Dictionary<string, string>();
        var entries = new List<string>();
        using var stream = File.OpenRead(filePath);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        metadata["EntryCount"] = zip.Entries.Count.ToString();
        foreach (var entry in zip.Entries.Take(50))
        {
            entries.Add(entry.FullName);
        }

        if (zip.Entries.Count > 50)
        {
            entries.Add($"... {zip.Entries.Count - 50} more");
        }

        return new FormatAnalysisResult(format, Path.GetFileName(filePath), new FileInfo(filePath).Length, metadata, Array.Empty<string>(), Array.Empty<string>(), entries);
    }

    private static FormatAnalysisResult AnalyzeAsar(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var metadata = new Dictionary<string, string>();
        var notes = new List<string>();
        if (bytes.Length < 8)
        {
            notes.Add("ASAR header too small.");
            return BasicResult(FileFormat.Asar, filePath, bytes.LongLength, metadata, notes);
        }

        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, 4));
        metadata["HeaderSize"] = headerSize.ToString();
        if (bytes.Length >= 8 + headerSize)
        {
            var json = Encoding.UTF8.GetString(bytes.AsSpan(8, headerSize));
            metadata["HeaderJsonLength"] = headerSize.ToString();
            metadata["HeaderStarts"] = json.Length > 32 ? json[..32] + "..." : json;
        }
        else
        {
            notes.Add("ASAR header truncated.");
        }

        return BasicResult(FileFormat.Asar, filePath, bytes.LongLength, metadata, notes);
    }

    private static FormatAnalysisResult AnalyzeScript(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var metadata = new Dictionary<string, string>();
        metadata["Encoding"] = "Text";
        var sample = Encoding.UTF8.GetString(bytes.Take(256).ToArray());
        return new FormatAnalysisResult(FileFormat.Script, Path.GetFileName(filePath), bytes.LongLength, metadata, Array.Empty<string>(), new[] { sample }, Array.Empty<string>());
    }

    private static FormatAnalysisResult AnalyzeUnknown(string filePath, FileTypeResult typeResult)
    {
        var bytes = File.ReadAllBytes(filePath);
        return BasicResult(typeResult.Format, filePath, bytes.LongLength, new Dictionary<string, string> { ["Description"] = typeResult.Description });
    }

    private static FormatAnalysisResult BasicResult(FileFormat format, string filePath, long size, IReadOnlyDictionary<string, string> metadata, IReadOnlyList<string>? notes = null)
    {
        return new FormatAnalysisResult(format, Path.GetFileName(filePath), size, metadata, notes ?? Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
    }

    private static ushort ReadUInt16(byte[] bytes, int offset, bool bigEndian)
    {
        return bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2)) : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    }

    private static uint ReadUInt32(byte[] bytes, int offset, bool bigEndian)
    {
        return bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)) : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static ulong ReadUInt64(byte[] bytes, int offset, bool bigEndian)
    {
        return bigEndian ? BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(offset, 8)) : BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, 8));
    }
}
