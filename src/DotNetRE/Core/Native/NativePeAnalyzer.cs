using System.Reflection.PortableExecutable;
using System.Text;
using Iced.Intel;
using PeNet;

namespace DotNetRE.Core.Native;

public sealed class NativePeAnalyzer
{
    public NativeAnalysisResult Analyze(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var peFile = new PeFile(filePath);

        var architecture = peFile.ImageNtHeaders?.FileHeader.Machine.ToString() ?? "Unknown";
        var entryPoint = peFile.ImageNtHeaders?.OptionalHeader?.AddressOfEntryPoint ?? 0;
        var fileSize = bytes.LongLength;

        var sections = BuildSectionInfo(peFile, bytes);
        var imports = BuildImportList(peFile);
        var exports = BuildExportList(peFile);
        var resources = BuildResourceInfo(peFile);
        var strings = ExtractStrings(bytes);
        var disassembly = DisassembleEntryPoint(bytes, peFile, entryPoint);
        var compilerHints = DetectCompilerHints(bytes, imports, sections);
        var packerHints = DetectPackerHints(bytes, sections, imports);

        return new NativeAnalysisResult(
            Path.GetFileName(filePath),
            architecture,
            $"0x{entryPoint:X8}",
            fileSize,
            sections,
            imports,
            exports,
            resources,
            strings,
            disassembly,
            compilerHints,
            packerHints);
    }

    private static IReadOnlyList<SectionInfo> BuildSectionInfo(PeFile peFile, byte[] fileBytes)
    {
        var list = new List<SectionInfo>();
        if (peFile.ImageSectionHeaders is null)
        {
            return list;
        }

        foreach (var section in peFile.ImageSectionHeaders)
        {
            var name = section.Name ?? string.Empty;
            var size = section.SizeOfRawData;
            var offset = section.PointerToRawData;
            var entropy = 0d;
            if (offset + size <= fileBytes.Length && size > 0)
            {
                entropy = CalculateEntropy(fileBytes.AsSpan((int)offset, (int)size));
            }

            list.Add(new SectionInfo(name.TrimEnd('\0'), section.VirtualSize, section.SizeOfRawData, entropy));
        }

        return list;
    }

    private static IReadOnlyList<string> BuildImportList(PeFile peFile)
    {
        var list = new List<string>();
        if (peFile.ImportedFunctions is null)
        {
            return list;
        }

        foreach (var import in peFile.ImportedFunctions)
        {
            var module = import.DLL ?? string.Empty;
            var name = import.Name ?? "Ordinal";
            list.Add($"{module}!{name}");
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
    }

    private static IReadOnlyList<string> BuildExportList(PeFile peFile)
    {
        var list = new List<string>();
        if (peFile.ExportedFunctions is null)
        {
            return list;
        }

        foreach (var export in peFile.ExportedFunctions)
        {
            if (!string.IsNullOrWhiteSpace(export.Name))
            {
                list.Add(export.Name!);
            }
            else
            {
                list.Add($"Ordinal:{export.Ordinal}");
            }
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
    }

    private static IReadOnlyList<ResourceInfo> BuildResourceInfo(PeFile peFile)
    {
        var list = new List<ResourceInfo>();
        var resources = peFile.Resources;
        if (resources is null)
        {
            return list;
        }

        var typesProperty = resources.GetType().GetProperty("Types") ?? resources.GetType().GetProperty("ResourceTypes");
        if (typesProperty?.GetValue(resources) is System.Collections.IEnumerable types)
        {
            foreach (var type in types)
            {
                var nameProp = type.GetType().GetProperty("Name");
                var idProp = type.GetType().GetProperty("ID") ?? type.GetType().GetProperty("Id");
                var resProp = type.GetType().GetProperty("Resources") ?? type.GetType().GetProperty("Entries");
                var name = nameProp?.GetValue(type)?.ToString() ?? idProp?.GetValue(type)?.ToString() ?? "Unknown";
                var count = 0;
                if (resProp?.GetValue(type) is System.Collections.IEnumerable entries)
                {
                    foreach (var _ in entries)
                    {
                        count++;
                    }
                }

                list.Add(new ResourceInfo(name, count));
            }
        }

        return list.OrderByDescending(r => r.Count).ToList();
    }

    private static IReadOnlyList<string> ExtractStrings(byte[] bytes, int minLength = 4, int maxCount = 200)
    {
        var results = new List<string>();
        ExtractAscii(bytes, minLength, results);
        ExtractUnicode(bytes, minLength, results);

        return results
            .Where(s => s.Length >= minLength)
            .Distinct()
            .OrderByDescending(s => s.Length)
            .Take(maxCount)
            .ToList();
    }

    private static void ExtractAscii(byte[] bytes, int minLength, List<string> results)
    {
        var buffer = new List<byte>();
        foreach (var b in bytes)
        {
            if (b >= 0x20 && b <= 0x7E)
            {
                buffer.Add(b);
                continue;
            }

            if (buffer.Count >= minLength)
            {
                results.Add(System.Text.Encoding.ASCII.GetString(buffer.ToArray()));
            }
            buffer.Clear();
        }

        if (buffer.Count >= minLength)
        {
            results.Add(System.Text.Encoding.ASCII.GetString(buffer.ToArray()));
        }
    }

    private static void ExtractUnicode(byte[] bytes, int minLength, List<string> results)
    {
        var buffer = new List<byte>();
        for (var i = 0; i < bytes.Length - 1; i += 2)
        {
            var lo = bytes[i];
            var hi = bytes[i + 1];
            if (hi == 0x00 && lo >= 0x20 && lo <= 0x7E)
            {
                buffer.Add(lo);
                continue;
            }

            if (buffer.Count >= minLength)
            {
                results.Add(System.Text.Encoding.ASCII.GetString(buffer.ToArray()));
            }
            buffer.Clear();
        }

        if (buffer.Count >= minLength)
        {
            results.Add(System.Text.Encoding.ASCII.GetString(buffer.ToArray()));
        }
    }

    private static IReadOnlyList<string> DisassembleEntryPoint(byte[] bytes, PeFile peFile, uint entryPointRva)
    {
        var offset = RvaToFileOffset(peFile, entryPointRva);
        if (offset < 0 || offset >= bytes.Length)
        {
            return new[] { "Entry point could not be resolved." };
        }

        var slice = bytes.AsSpan(offset, Math.Min(64, bytes.Length - offset)).ToArray();
        var bitness = peFile.Is64Bit ? 64 : 32;
        var decoder = Iced.Intel.Decoder.Create(bitness, new ByteArrayCodeReader(slice));
        decoder.IP = entryPointRva;

        var formatter = new NasmFormatter();
        var output = new StringBuilder();
        var formatterOutput = new StringBuilderFormatterOutput(output);
        var lines = new List<string>();
        var instruction = new Instruction();
        for (var i = 0; i < 12; i++)
        {
            decoder.Decode(out instruction);
            if (instruction.IsInvalid)
            {
                break;
            }
            output.Clear();
            formatter.Format(instruction, formatterOutput);
            lines.Add($"0x{instruction.IP:X8}  {output}");
        }

        return lines;
    }

    private sealed class StringBuilderFormatterOutput : FormatterOutput
    {
        private readonly StringBuilder _builder;

        public StringBuilderFormatterOutput(StringBuilder builder)
        {
            _builder = builder;
        }

        public override void Write(string text, FormatterTextKind kind)
        {
            _builder.Append(text);
        }
    }

    private static int RvaToFileOffset(PeFile peFile, uint rva)
    {
        if (peFile.ImageSectionHeaders is null)
        {
            return -1;
        }

        foreach (var section in peFile.ImageSectionHeaders)
        {
            var start = section.VirtualAddress;
            var end = start + Math.Max(section.VirtualSize, section.SizeOfRawData);
            if (rva >= start && rva < end)
            {
                return (int)(rva - start + section.PointerToRawData);
            }
        }

        return -1;
    }

    private static double CalculateEntropy(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
        {
            return 0;
        }

        Span<int> counts = stackalloc int[256];
        foreach (var b in data)
        {
            counts[b]++;
        }

        double entropy = 0;
        foreach (var count in counts)
        {
            if (count == 0)
            {
                continue;
            }

            var p = (double)count / data.Length;
            entropy -= p * Math.Log2(p);
        }

        return Math.Round(entropy, 3, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyList<string> DetectCompilerHints(byte[] bytes, IReadOnlyList<string> imports, IReadOnlyList<SectionInfo> sections)
    {
        var hints = new List<string>();
        var text = System.Text.Encoding.ASCII.GetString(bytes);

        if (imports.Any(i => i.Contains("msvcrt", StringComparison.OrdinalIgnoreCase) ||
                             i.Contains("vcruntime", StringComparison.OrdinalIgnoreCase)))
        {
            hints.Add("MSVC runtime detected");
        }

        if (imports.Any(i => i.Contains("libgcc", StringComparison.OrdinalIgnoreCase) ||
                             i.Contains("libstdc++", StringComparison.OrdinalIgnoreCase)))
        {
            hints.Add("GCC/MinGW runtime detected");
        }

        if (text.Contains("Go build ID", StringComparison.OrdinalIgnoreCase) ||
            sections.Any(s => s.Name.Equals(".gopclntab", StringComparison.OrdinalIgnoreCase)))
        {
            hints.Add("Go toolchain detected");
        }

        if (text.Contains("rust_eh_personality", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("Rust toolchain detected");
        }

        if (text.Contains("ReadyToRun", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add(".NET ReadyToRun image detected");
        }

        if (text.Contains("NativeAOT", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add(".NET NativeAOT markers detected");
        }

        if (text.Contains("Native Image", StringComparison.OrdinalIgnoreCase) ||
            text.Contains(".ni.dll", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add(".NET NGen native image detected");
        }

        if (text.Contains("Delphi", StringComparison.OrdinalIgnoreCase) ||
            imports.Any(i => i.Contains("rtl", StringComparison.OrdinalIgnoreCase)))
        {
            hints.Add("Delphi runtime detected");
        }

        return hints;
    }

    private static IReadOnlyList<string> DetectPackerHints(byte[] bytes, IReadOnlyList<SectionInfo> sections, IReadOnlyList<string> imports)
    {
        var hints = new List<string>();
        var text = System.Text.Encoding.ASCII.GetString(bytes);

        if (sections.Any(s => s.Name.StartsWith("UPX", StringComparison.OrdinalIgnoreCase)) ||
            text.Contains("UPX!", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("UPX packer detected");
        }

        if (sections.Any(s => s.Name.Contains(".vmp", StringComparison.OrdinalIgnoreCase)) ||
            text.Contains("VMProtect", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("VMProtect detected");
        }

        if (text.Contains("Themida", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("Themida detected");
        }

        if (text.Contains("Obsidium", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("Obsidium detected");
        }

        if (text.Contains("PyInstaller", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("MEIPASS", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("PyInstaller packer detected");
        }

        if (text.Contains("Electron", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("ASAR", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("Electron app detected");
        }

        if (text.Contains("AutoIt", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("AutoIt packaging detected");
        }

        if (sections.Any(s => s.Entropy >= 7.4))
        {
            hints.Add("High-entropy sections (possible packing)");
        }

        if (imports.Any(i => i.Contains("VirtualAlloc", StringComparison.OrdinalIgnoreCase)) &&
            imports.Any(i => i.Contains("VirtualProtect", StringComparison.OrdinalIgnoreCase)))
        {
            hints.Add("Runtime unpacking patterns (VirtualAlloc/VirtualProtect)");
        }

        return hints;
    }
}
