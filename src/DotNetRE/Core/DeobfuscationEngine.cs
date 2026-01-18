using dnlib.DotNet;
using DotNetRE.Core.AntiAnti;
using DotNetRE.Core.Deobfuscators;

namespace DotNetRE.Core;

public sealed class DeobfuscationEngine
{
    private readonly IReadOnlyList<IAntiAntiModule> _antiAntiModules;
    private readonly IReadOnlyList<IDeobfuscator> _deobfuscators;

    public DeobfuscationEngine()
    {
        _antiAntiModules = new IAntiAntiModule[]
        {
            new AntiTamperRemover(),
            new AntiDebugRemover(),
            new InvalidMetadataFixer(),
            new ProxyCallResolver(),
            new ControlFlowDeobfuscator()
        };

        _deobfuscators = new IDeobfuscator[]
        {
            new StringDecryptor(),
            new RenameMapper(),
            new ResourceDecryptor(),
            new ConstantFolder()
        };
    }

    public DeobfuscationReport Run(ModuleDefMD module)
    {
        var antiAntiResults = RunAntiAnti(module).ToList();
        var deobfuscationResults = RunDeobfuscators(module).ToList();
        return new DeobfuscationReport(antiAntiResults, deobfuscationResults);
    }

    public IReadOnlyList<AntiAntiResult> RunAntiAnti(ModuleDefMD module)
    {
        return _antiAntiModules.Select(m => m.Apply(module)).ToList();
    }

    public IReadOnlyList<DeobfuscationResult> RunDeobfuscators(ModuleDefMD module)
    {
        return _deobfuscators.Select(d => d.Apply(module)).ToList();
    }
}

public sealed record DeobfuscationReport(
    IReadOnlyList<AntiAntiResult> AntiAntiResults,
    IReadOnlyList<DeobfuscationResult> DeobfuscationResults
);
