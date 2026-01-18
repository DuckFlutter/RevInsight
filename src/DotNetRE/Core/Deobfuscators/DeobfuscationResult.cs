namespace DotNetRE.Core.Deobfuscators;

public sealed record DeobfuscationResult(string Name, int Changes, string Notes);

public interface IDeobfuscator
{
    string Name { get; }
    DeobfuscationResult Apply(dnlib.DotNet.ModuleDefMD module);
}
