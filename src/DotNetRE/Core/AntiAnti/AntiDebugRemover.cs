using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace DotNetRE.Core.AntiAnti;

public sealed class AntiDebugRemover : IAntiAntiModule
{
    public string Name => "Anti-Debug";

    public AntiAntiResult Apply(ModuleDefMD module)
    {
        var changes = 0;
        foreach (var method in module.GetTypes().SelectMany(t => t.Methods))
        {
            if (!method.HasBody)
            {
                continue;
            }

            var instructions = method.Body.Instructions;
            for (var i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];
                if ((instr.OpCode == OpCodes.Call || instr.OpCode == OpCodes.Callvirt) &&
                    instr.Operand is IMethod target)
                {
                    var fullName = target.FullName;
                    if (fullName.Contains("System.Diagnostics.Debugger::get_IsAttached", StringComparison.OrdinalIgnoreCase) ||
                        fullName.Contains("CheckRemoteDebuggerPresent", StringComparison.OrdinalIgnoreCase) ||
                        fullName.Contains("IsDebuggerPresent", StringComparison.OrdinalIgnoreCase))
                    {
                        instructions[i] = Instruction.Create(OpCodes.Ldc_I4_0);
                        changes++;
                    }
                }
            }
        }

        var notes = changes == 0
            ? "No anti-debug calls replaced."
            : "Replaced anti-debug calls with constant false.";
        return new AntiAntiResult(Name, changes, notes);
    }
}
