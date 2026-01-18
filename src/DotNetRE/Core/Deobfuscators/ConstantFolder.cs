using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace DotNetRE.Core.Deobfuscators;

public sealed class ConstantFolder : IDeobfuscator
{
    public string Name => "Constant Folder";

    public DeobfuscationResult Apply(ModuleDefMD module)
    {
        var changes = 0;
        foreach (var method in module.GetTypes().SelectMany(t => t.Methods))
        {
            if (!method.HasBody)
            {
                continue;
            }

            var instructions = method.Body.Instructions;
            for (var i = 0; i < instructions.Count - 2; i++)
            {
                if (!IsLdcI4(instructions[i], out var a))
                {
                    continue;
                }

                if (!IsLdcI4(instructions[i + 1], out var b))
                {
                    continue;
                }

                var op = instructions[i + 2].OpCode;
                int? folded = op.Code switch
                {
                    Code.Add => a + b,
                    Code.Sub => a - b,
                    Code.Mul => a * b,
                    Code.Xor => a ^ b,
                    Code.And => a & b,
                    Code.Or => a | b,
                    _ => null
                };

                if (folded is null)
                {
                    continue;
                }

                instructions[i] = Instruction.CreateLdcI4(folded.Value);
                instructions[i + 1] = Instruction.Create(OpCodes.Nop);
                instructions[i + 2] = Instruction.Create(OpCodes.Nop);
                changes++;
            }
        }

        var notes = changes == 0
            ? "No constant folds applied."
            : "Folded simple arithmetic constants.";
        return new DeobfuscationResult(Name, changes, notes);
    }

    private static bool IsLdcI4(Instruction instruction, out int value)
    {
        value = 0;
        if (instruction.OpCode == OpCodes.Ldc_I4)
        {
            value = (int)instruction.Operand!;
            return true;
        }

        if (instruction.OpCode == OpCodes.Ldc_I4_S)
        {
            value = (sbyte)instruction.Operand!;
            return true;
        }

        switch (instruction.OpCode.Code)
        {
            case Code.Ldc_I4_0:
                value = 0; return true;
            case Code.Ldc_I4_1:
                value = 1; return true;
            case Code.Ldc_I4_2:
                value = 2; return true;
            case Code.Ldc_I4_3:
                value = 3; return true;
            case Code.Ldc_I4_4:
                value = 4; return true;
            case Code.Ldc_I4_5:
                value = 5; return true;
            case Code.Ldc_I4_6:
                value = 6; return true;
            case Code.Ldc_I4_7:
                value = 7; return true;
            case Code.Ldc_I4_8:
                value = 8; return true;
            case Code.Ldc_I4_M1:
                value = -1; return true;
            default:
                return false;
        }
    }
}
