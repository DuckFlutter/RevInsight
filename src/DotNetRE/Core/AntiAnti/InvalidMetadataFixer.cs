using System.Text;
using dnlib.DotNet;

namespace DotNetRE.Core.AntiAnti;

public sealed class InvalidMetadataFixer : IAntiAntiModule
{
    public string Name => "Invalid Metadata";

    public AntiAntiResult Apply(ModuleDefMD module)
    {
        var changes = 0;

        foreach (var type in module.GetTypes())
        {
            var sanitizedName = SanitizeIdentifier(type.Name, "Type");
            if (!string.Equals(type.Name, sanitizedName, StringComparison.Ordinal))
            {
                type.Name = sanitizedName;
                changes++;
            }

            var sanitizedNamespace = SanitizeIdentifier(type.Namespace, string.Empty, allowEmpty: true);
            if (!string.Equals(type.Namespace, sanitizedNamespace, StringComparison.Ordinal))
            {
                type.Namespace = sanitizedNamespace;
                changes++;
            }

            foreach (var method in type.Methods)
            {
                var sanitizedMethod = SanitizeIdentifier(method.Name, "Method");
                if (!string.Equals(method.Name, sanitizedMethod, StringComparison.Ordinal))
                {
                    method.Name = sanitizedMethod;
                    changes++;
                }

                foreach (var param in method.Parameters)
                {
                    if (param.IsHiddenThisParameter)
                    {
                        continue;
                    }

                    var sanitizedParam = SanitizeIdentifier(param.Name, "param", allowEmpty: true);
                    if (!string.Equals(param.Name, sanitizedParam, StringComparison.Ordinal))
                    {
                        param.Name = sanitizedParam;
                        changes++;
                    }
                }
            }

            foreach (var field in type.Fields)
            {
                var sanitizedField = SanitizeIdentifier(field.Name, "Field");
                if (!string.Equals(field.Name, sanitizedField, StringComparison.Ordinal))
                {
                    field.Name = sanitizedField;
                    changes++;
                }
            }

            foreach (var prop in type.Properties)
            {
                var sanitizedProp = SanitizeIdentifier(prop.Name, "Property");
                if (!string.Equals(prop.Name, sanitizedProp, StringComparison.Ordinal))
                {
                    prop.Name = sanitizedProp;
                    changes++;
                }
            }

            foreach (var evt in type.Events)
            {
                var sanitizedEvent = SanitizeIdentifier(evt.Name, "Event");
                if (!string.Equals(evt.Name, sanitizedEvent, StringComparison.Ordinal))
                {
                    evt.Name = sanitizedEvent;
                    changes++;
                }
            }
        }

        var notes = changes == 0
            ? "No invalid metadata identifiers sanitized."
            : "Sanitized invalid metadata identifiers.";
        return new AntiAntiResult(Name, changes, notes);
    }

    private static string SanitizeIdentifier(string? value, string fallback, bool allowEmpty = false)
    {
        var input = value ?? string.Empty;
        var builder = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (char.IsControl(ch) || ch == '\uFFFD')
            {
                continue;
            }

            builder.Append(ch);
        }

        var sanitized = builder.ToString().Trim();
        if (allowEmpty && string.IsNullOrEmpty(sanitized))
        {
            return string.Empty;
        }

        return string.IsNullOrEmpty(sanitized) ? fallback : sanitized;
    }
}
