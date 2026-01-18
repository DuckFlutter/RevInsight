using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace DotNetRE.Core.AntiAnti;

public sealed class ProxyCallResolver : IAntiAntiModule
{
    public string Name => "Proxy Calls";

    public AntiAntiResult Apply(ModuleDefMD module)
    {
        var proxyMap = BuildProxyMap(module);
        var changes = 0;

        if (proxyMap.Count == 0)
        {
            return new AntiAntiResult(Name, 0, "No proxy methods identified.");
        }

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
                    instr.Operand is MethodDef proxy &&
                    proxyMap.TryGetValue(proxy, out var target))
                {
                    instr.Operand = target;
                    instr.OpCode = target.ResolveMethodDef()?.IsVirtual == true ? OpCodes.Callvirt : OpCodes.Call;
                    changes++;
                }
            }
        }

        var notes = changes == 0
            ? "No proxy calls resolved."
            : "Inlined proxy calls to their target methods.";
        return new AntiAntiResult(Name, changes, notes);
    }

    private static Dictionary<MethodDef, IMethod> BuildProxyMap(ModuleDefMD module)
    {
        var map = new Dictionary<MethodDef, IMethod>();

        foreach (var method in module.GetTypes().SelectMany(t => t.Methods))
        {
            if (!method.HasBody || method.Body.Instructions.Count < 3)
            {
                continue;
            }

            var instructions = method.Body.Instructions
                .Where(instr => instr.OpCode != OpCodes.Nop)
                .ToList();

            if (instructions.Count < 3)
            {
                continue;
            }

            if (instructions[^1].OpCode != OpCodes.Ret)
            {
                continue;
            }

            var callInstr = instructions[^2];
            if (callInstr.OpCode != OpCodes.Call && callInstr.OpCode != OpCodes.Callvirt)
            {
                continue;
            }

            if (callInstr.Operand is not IMethod target)
            {
                continue;
            }

            var paramCount = method.Parameters.Count;
            if (instructions.Count != paramCount + 2)
            {
                continue;
            }

            var matches = true;
            for (var i = 0; i < paramCount; i++)
            {
                if (!IsLdarg(instructions[i], i))
                {
                    matches = false;
                    break;
                }
            }

            if (!matches)
            {
                continue;
            }

            if (target.MethodSig is null || method.MethodSig is null)
            {
                continue;
            }

            if (target.MethodSig.Params.Count != method.MethodSig.Params.Count ||
                target.MethodSig.HasThis != method.MethodSig.HasThis)
            {
                continue;
            }

            map[method] = target;
        }

        return map;
    }

    private static bool IsLdarg(Instruction instruction, int index)
    {
        return instruction.OpCode.Code switch
        {
            Code.Ldarg_0 => index == 0,
            Code.Ldarg_1 => index == 1,
            Code.Ldarg_2 => index == 2,
            Code.Ldarg_3 => index == 3,
            Code.Ldarg_S => instruction.Operand is Parameter param && param.Index == index,
            Code.Ldarg => instruction.Operand is Parameter param2 && param2.Index == index,
            _ => false
        };
    }
}
