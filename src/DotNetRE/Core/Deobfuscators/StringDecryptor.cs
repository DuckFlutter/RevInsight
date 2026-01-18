using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace DotNetRE.Core.Deobfuscators;

public sealed class StringDecryptor : IDeobfuscator
{
    public string Name => "String Decryptor";

    public DeobfuscationResult Apply(ModuleDefMD module)
    {
        var changes = 0;
        foreach (var method in module.GetTypes().SelectMany(t => t.Methods))
        {
            if (!method.HasBody)
            {
                continue;
            }

            foreach (var instr in method.Body.Instructions)
            {
                if (instr.OpCode == OpCodes.Ldstr && instr.Operand is string value)
                {
                    if (IsBase64(value, out var decoded) && IsMostlyPrintable(decoded))
                    {
                        instr.Operand = decoded;
                        changes++;
                    }
                }
            }
        }

        var notes = changes == 0
            ? "No Base64 strings replaced."
            : "Replaced Base64-encoded literals with decoded strings.";
        return new DeobfuscationResult(Name, changes, notes);
    }

    private static bool IsBase64(string value, out string decoded)
    {
        decoded = string.Empty;
        if (value.Length < 8 || value.Length % 4 != 0)
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var bytesWritten))
        {
            return false;
        }

        decoded = Encoding.UTF8.GetString(buffer[..bytesWritten]);
        return true;
    }

    private static bool IsMostlyPrintable(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var printable = value.Count(ch => !char.IsControl(ch));
        return printable >= value.Length * 0.8;
    }
}
