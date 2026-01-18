using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace DotNetRE.Core.AntiAnti;

public sealed class ControlFlowDeobfuscator : IAntiAntiModule
{
    public string Name => "Control Flow";

    public AntiAntiResult Apply(ModuleDefMD module)
    {
        var changes = 0;
        foreach (var method in module.GetTypes().SelectMany(t => t.Methods))
        {
            if (!method.HasBody)
            {
                continue;
            }

            var body = method.Body;
            var referenced = new HashSet<Instruction>();

            foreach (var instr in body.Instructions)
            {
                if (instr.Operand is Instruction target)
                {
                    referenced.Add(target);
                }
                else if (instr.Operand is Instruction[] targets)
                {
                    foreach (var t in targets)
                    {
                        referenced.Add(t);
                    }
                }
            }

            foreach (var handler in body.ExceptionHandlers)
            {
                if (handler.TryStart is not null) referenced.Add(handler.TryStart);
                if (handler.TryEnd is not null) referenced.Add(handler.TryEnd);
                if (handler.HandlerStart is not null) referenced.Add(handler.HandlerStart);
                if (handler.HandlerEnd is not null) referenced.Add(handler.HandlerEnd);
                if (handler.FilterStart is not null) referenced.Add(handler.FilterStart);
            }

            for (var i = body.Instructions.Count - 1; i >= 0; i--)
            {
                var instr = body.Instructions[i];
                if (instr.OpCode == OpCodes.Nop && !referenced.Contains(instr))
                {
                    body.Instructions.RemoveAt(i);
                    changes++;
                }
            }

            var before = body.Instructions.Count;
            body.SimplifyBranches();
            body.OptimizeBranches();
            body.OptimizeMacros();
            var after = body.Instructions.Count;
            if (before != after)
            {
                changes += Math.Abs(before - after);
            }
        }

        var notes = changes == 0
            ? "No control flow simplifications applied."
            : "Simplified branches and removed dead NOPs.";
        return new AntiAntiResult(Name, changes, notes);
    }
}
