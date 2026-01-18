using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace DotNetRE.Core;

public enum FindingSeverity
{
    Clean,
    Warning,
    Obfuscated
}

public sealed record AnalysisFinding(string Title, string Details, FindingSeverity Severity);

public sealed record AssemblyMetadata(
    string Name,
    string RuntimeVersion,
    int TypeCount,
    int MethodCount,
    long FileSize,
    double Entropy
);

public sealed record AnalysisResult(
    AssemblyMetadata Metadata,
    IReadOnlyList<AnalysisFinding> Findings,
    bool ObfuscationDetected
);

public sealed class AssemblyAnalyzer
{
    public AnalysisResult Analyze(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException("Assembly not found.", assemblyPath);
        }

        var fileBytes = File.ReadAllBytes(assemblyPath);
        var entropy = CalculateEntropy(fileBytes);
        var module = ModuleDefMD.Load(fileBytes);

        var findings = new List<AnalysisFinding>();
        if (entropy >= 7.2)
        {
            findings.Add(new AnalysisFinding(
                "High entropy",
                "Binary entropy is high, suggesting compression or packing.",
                FindingSeverity.Warning));
        }

        if (ContainsUnicodeIdentifiers(module))
        {
            findings.Add(new AnalysisFinding(
                "Unicode identifiers",
                "Type or member names contain non-ASCII characters.",
                FindingSeverity.Warning));
        }

        if (ContainsAntiDebugChecks(module))
        {
            findings.Add(new AnalysisFinding(
                "Anti-debug checks",
                "Detected debug-check patterns like Debugger.IsAttached.",
                FindingSeverity.Obfuscated));
        }

        var protector = DetectProtector(module);
        if (!string.IsNullOrWhiteSpace(protector))
        {
            findings.Add(new AnalysisFinding(
                "Protector signature",
                $"Detected markers for {protector}.",
                FindingSeverity.Obfuscated));
        }

        var metadata = new AssemblyMetadata(
            module.Assembly?.Name ?? Path.GetFileNameWithoutExtension(assemblyPath),
            module.RuntimeVersion,
            module.Types.Count,
            module.GetTypes().Sum(t => t.Methods.Count),
            fileBytes.LongLength,
            entropy);

        var obfuscationDetected = findings.Any(f => f.Severity != FindingSeverity.Clean);
        return new AnalysisResult(metadata, findings, obfuscationDetected);
    }

    private static bool ContainsUnicodeIdentifiers(ModuleDefMD module)
    {
        return module.GetTypes().Any(t => HasUnicode(t.Name) || HasUnicode(t.Namespace) ||
                                         t.Methods.Any(m => HasUnicode(m.Name)));
    }

    private static bool HasUnicode(string value)
    {
        return value.Any(ch => ch > 0x7F);
    }

    private static bool ContainsAntiDebugChecks(ModuleDefMD module)
    {
        foreach (var method in module.GetTypes().SelectMany(t => t.Methods))
        {
            if (!method.HasBody)
            {
                continue;
            }

            foreach (var instr in method.Body.Instructions)
            {
                if (instr.OpCode == OpCodes.Call || instr.OpCode == OpCodes.Callvirt)
                {
                    if (instr.Operand is IMethod target)
                    {
                        var fullName = target.FullName;
                        if (fullName.Contains("System.Diagnostics.Debugger::get_IsAttached", StringComparison.OrdinalIgnoreCase) ||
                            fullName.Contains("CheckRemoteDebuggerPresent", StringComparison.OrdinalIgnoreCase) ||
                            fullName.Contains("IsDebuggerPresent", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    private static string? DetectProtector(ModuleDefMD module)
    {
        var markers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConfuserEx"] = "ConfuserEx",
            ["ConfusedBy"] = "ConfuserEx",
            ["Eziriz"] = ".NET Reactor",
            ["Dotfuscator"] = "Dotfuscator",
            ["Babel"] = "Babel Obfuscator",
            ["AgileDotNet"] = "Agile.NET"
        };

        var searchSpace = new StringBuilder();
        searchSpace.Append(module.Assembly?.FullName);

        foreach (var attr in module.Assembly?.CustomAttributes ?? Enumerable.Empty<CustomAttribute>())
        {
            searchSpace.Append(attr.TypeFullName);
        }

        foreach (var res in module.Resources)
        {
            searchSpace.Append(res.Name);
        }

        foreach (var type in module.Types)
        {
            searchSpace.Append(type.FullName);
        }

        var text = searchSpace.ToString();
        foreach (var marker in markers)
        {
            if (text.Contains(marker.Key, StringComparison.OrdinalIgnoreCase))
            {
                return marker.Value;
            }
        }

        return null;
    }

    private static double CalculateEntropy(byte[] data)
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
}
