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
    public static bool Calls(MethodBase method, string typeFullName) =>
        Callees(method).Any(m => m.DeclaringType?.FullName == typeFullName);

    /// <summary>
    /// The first type matching <paramref name="match"/> whose constructor <paramref name="method"/> calls (newobj or a
    /// base constructor call), directly or through the methods it calls in assemblies <paramref name="follow"/> accepts.
    /// </summary>
    public static Type? Constructs(MethodBase method, Func<Type, bool> match, Func<Assembly, bool> follow)
    {
        var seen = new HashSet<MethodBase>();
        var pending = new Stack<MethodBase>([method]);
        while (pending.Count > 0 && seen.Count < MaxMethods)
        {
            var current = pending.Pop();
            if (!seen.Add(current))
            {
                continue;
            }

            foreach (var callee in Callees(current))
            {
                if (callee is ConstructorInfo { DeclaringType: { } type } && Safe(() => match(type)))
                {
                    return type;
                }

                if (callee.DeclaringType?.Assembly is { } assembly && follow(assembly))
                {
                    pending.Push(callee);
                }
            }
        }

        return null;
    }

    // Bounds the walk through project code; a deeper chain is not claimed.
    private const int MaxMethods = 2000;

    private static bool Safe(Func<bool> test)
    {
        try
        {
            return test();
        }
        catch (Exception e) when (e is TypeLoadException or FileNotFoundException or FileLoadException)
        {
            return false;
        }
    }

    private static IEnumerable<MethodBase> Callees(MethodBase method)
    {
        byte[]? il;
        try
        {
            il = method.GetMethodBody()?.GetILAsByteArray();
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or BadImageFormatException)
        {
            yield break;
        }

        if (il is null)
        {
            yield break;
        }

        var i = 0;
        while (i < il.Length)
        {
            short value = il[i] == 0xFE && i + 1 < il.Length ? (short)(0xFE00 | il[++i]) : il[i];
            i++;
            if (!OpCodesByValue.TryGetValue(value, out var op))
            {
                yield break; // not IL we understand: no claim
            }

            if (op.OperandType == OperandType.InlineMethod && i + 4 <= il.Length && Resolve(method, BitConverter.ToInt32(il, i)) is { } callee)
            {
                yield return callee;
            }

            i += OperandSize(op, il, i);
        }
    }

    private static MethodBase? Resolve(MethodBase method, int token)
    {
        try
        {
            var typeArgs = method.DeclaringType is { IsGenericType: true } t ? t.GetGenericArguments() : null;
            var methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;
            return method.Module.ResolveMethod(token, typeArgs, methodArgs);
        }
        catch (Exception e) when (e is ArgumentException or BadImageFormatException or TypeLoadException or FileNotFoundException or FileLoadException or MissingMethodException)
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
