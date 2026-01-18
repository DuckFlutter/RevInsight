namespace DotNetRE.Core.AntiAnti;

public sealed record AntiAntiResult(string Name, int Changes, string Notes);

public interface IAntiAntiModule
{
    string Name { get; }
    AntiAntiResult Apply(dnlib.DotNet.ModuleDefMD module);
}
