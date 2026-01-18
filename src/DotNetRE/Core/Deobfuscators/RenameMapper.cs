using dnlib.DotNet;

namespace DotNetRE.Core.Deobfuscators;

public sealed class RenameMapper : IDeobfuscator
{
    public string Name => "Rename Mapper";

    public DeobfuscationResult Apply(ModuleDefMD module)
    {
        var changes = 0;
        var typeIndex = 1;
        var methodIndex = 1;

        foreach (var type in module.GetTypes())
        {
            if (type.IsGlobalModuleType)
            {
                continue;
            }

            if (IsObfuscated(type.Name))
            {
                type.Name = $"Class{typeIndex:D3}";
                typeIndex++;
                changes++;
            }

            foreach (var method in type.Methods)
            {
                if (method.IsConstructor || method.IsStaticConstructor)
                {
                    continue;
                }

                if (IsObfuscated(method.Name))
                {
                    method.Name = $"Method{methodIndex:D3}";
                    methodIndex++;
                    changes++;
                }
            }
        }

        var notes = changes == 0
            ? "No symbols renamed."
            : "Replaced obfuscated identifiers with readable names.";
        return new DeobfuscationResult(Name, changes, notes);
    }

    private static bool IsObfuscated(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.Length <= 2 || name.Any(ch => ch > 0x7F) || name.All(ch => ch == '_' || ch == '$');
    }
}
