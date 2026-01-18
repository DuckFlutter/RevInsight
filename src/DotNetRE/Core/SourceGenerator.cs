using System.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.Output;
using DotNetRE.Core.Output;

namespace DotNetRE.Core;

public sealed class SourceGenerator
{
    public void Generate(string assemblyPath, string outputDirectory, OutputFormat format)
    {
        Directory.CreateDirectory(outputDirectory);
        switch (format)
        {
            case OutputFormat.Cs:
                GenerateProject(assemblyPath, outputDirectory);
                break;
            case OutputFormat.Single:
                GenerateSingleFile(assemblyPath, outputDirectory);
                break;
            case OutputFormat.Il:
                GenerateIl(assemblyPath, outputDirectory);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    private static void GenerateProject(string assemblyPath, string outputDirectory)
    {
        var settings = new DecompilerSettings(LanguageVersion.Latest)
        {
            DecompileMemberBodies = true
        };
                var decompiler = new CSharpDecompiler(assemblyPath, settings);

                foreach (var type in decompiler.TypeSystem.MainModule.TypeDefinitions)
                {
                        if (type.FullName == "<Module>")
                        {
                                continue;
                        }

                        var code = decompiler.DecompileTypeAsString(type.FullTypeName);
                        var ns = type.Namespace ?? string.Empty;
                        var folder = string.IsNullOrWhiteSpace(ns)
                                ? outputDirectory
                                : Path.Combine(outputDirectory, ns.Replace('.', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(folder);
                        var fileName = SanitizeFileName(type.Name) + ".cs";
                        File.WriteAllText(Path.Combine(folder, fileName), code, Encoding.UTF8);
                }

                var name = Path.GetFileNameWithoutExtension(assemblyPath);
                var projectPath = Path.Combine(outputDirectory, $"{name}.csproj");
                var project = $"""
<Project Sdk=\"Microsoft.NET.Sdk\">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>enable</Nullable>
    </PropertyGroup>
    <ItemGroup>
        <Compile Include=\"**\\*.cs\" />
    </ItemGroup>
</Project>
""";
                File.WriteAllText(projectPath, project, Encoding.UTF8);
    }

    private static void GenerateSingleFile(string assemblyPath, string outputDirectory)
    {
        var settings = new DecompilerSettings(LanguageVersion.Latest);
        var decompiler = new CSharpDecompiler(assemblyPath, settings);
        var code = decompiler.DecompileWholeModuleAsString();

        var name = Path.GetFileNameWithoutExtension(assemblyPath);
        var outputPath = Path.Combine(outputDirectory, $"{name}.cs");
        File.WriteAllText(outputPath, code, Encoding.UTF8);
    }

    private static void GenerateIl(string assemblyPath, string outputDirectory)
    {
        var outputPath = Path.Combine(outputDirectory, "module.il");
        using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        var output = new PlainTextOutput(writer);
        using var peFile = new PEFile(assemblyPath);
        var disassembler = new ReflectionDisassembler(output, CancellationToken.None);
        disassembler.WriteModuleContents(peFile);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Type" : sanitized;
    }
}
