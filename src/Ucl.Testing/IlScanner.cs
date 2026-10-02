using System.Reflection;
using System.Reflection.Emit;

namespace Ucl.Testing;

/// <summary>Decodes a method's IL to find the methods it calls (call, callvirt, newobj, ldftn), using the runtime's opcode table.</summary>
internal static class IlScanner
{
    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => o.Value);

    /// <summary>True when <paramref name="method"/>'s body calls a member declared on a type named <paramref name="typeFullName"/>.</summary>
    public static bool Calls(MethodBase method, string typeFullName)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            return false;
        }

        var i = 0;
        while (i < il.Length)
        {
            short value = il[i] == 0xFE && i + 1 < il.Length ? (short)(0xFE00 | il[++i]) : il[i];
            i++;
            if (!OpCodesByValue.TryGetValue(value, out var op))
            {
                return false; // not IL we understand: no claim
            }

            if (op.OperandType == OperandType.InlineMethod && i + 4 <= il.Length)
            {
                var token = BitConverter.ToInt32(il, i);
                if (Resolve(method.Module, token) is { DeclaringType: { } declaring } && declaring.FullName == typeFullName)
                {
                    return true;
                }
            }

            i += OperandSize(op, il, i);
        }

        return false;
    }

    private static MethodBase? Resolve(Module module, int token)
    {
        try
        {
            return module.ResolveMethod(token);
        }
        catch (Exception e) when (e is ArgumentException or BadImageFormatException or TypeLoadException or FileNotFoundException or MissingMethodException)
        {
            return null;
        }
    }

    private static int OperandSize(OpCode op, byte[] il, int at) => op.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, at)),
        _ => 4,
    };
}
