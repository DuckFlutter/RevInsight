using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace DotNetRE.Core.AntiAnti;

public sealed class AntiTamperRemover : IAntiAntiModule
{
    public string Name => "Anti-Tamper";

    public AntiAntiResult Apply(ModuleDefMD module)
    {
        var changes = 0;
        foreach (var type in module.GetTypes())
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody)
                {
                    continue;
                }

                for (var i = 0; i < method.Body.Instructions.Count; i++)
                {
                    var instr = method.Body.Instructions[i];
                    if ((instr.OpCode == OpCodes.Call || instr.OpCode == OpCodes.Callvirt) &&
                        instr.Operand is IMethod target)
                    {
                        var methodName = target.Name?.String ?? target.Name?.ToString() ?? string.Empty;
                        var typeName = target.DeclaringType?.FullName ?? string.Empty;
                        if (methodName.IndexOf("Initialize", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            typeName.IndexOf("AntiTamper", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            method.Body.Instructions[i] = Instruction.Create(OpCodes.Nop);
                            changes++;
                        }
                    }
                }
            }
        }

        var notes = changes == 0
            ? "No anti-tamper markers removed."
            : "Replaced anti-tamper calls with NOPs.";
        return new AntiAntiResult(Name, changes, notes);
    }
}
