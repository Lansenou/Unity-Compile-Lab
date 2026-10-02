// A self-written stand-in for the core types of Unity's .NET Framework 4.8 profile (unity-4.8-api/mscorlib.dll).
// It declares only what the fixtures and the stub editor modules use, and deliberately has no System.Span<T>.

namespace System
{
    public class Object
    {
        public Object() { }
        public virtual bool Equals(object obj) => false;
        public virtual int GetHashCode() => 0;
        public virtual string ToString() => null;
        public Type GetType() => null;
        ~Object() { }
    }

    public abstract class ValueType { }
    public abstract class Enum : ValueType { }
    public struct Void { }
    public struct Boolean { }
    public struct Char { }
    public struct SByte { }
    public struct Byte { }
    public struct Int16 { }
    public struct UInt16 { }
    public struct Int32 { }
    public struct UInt32 { }
    public struct Int64 { }
    public struct UInt64 { }
    public struct Single
    {
        public string ToString(IFormatProvider provider) => null;
    }
    public struct Double
    {
        public string ToString(IFormatProvider provider) => null;
    }

    public interface IFormatProvider
    {
        object GetFormat(Type formatType);
    }
    public struct Decimal { }
    public struct IntPtr
    {
        public static readonly IntPtr Zero;
        public static bool operator ==(IntPtr a, IntPtr b) => false;
        public static bool operator !=(IntPtr a, IntPtr b) => false;
        public override bool Equals(object obj) => false;
        public override int GetHashCode() => 0;
    }
    public struct UIntPtr { }
    public struct Nullable<T> where T : struct
    {
        public bool HasValue => false;
        public T Value => default;
        public T GetValueOrDefault() => default;
    }

    public struct RuntimeTypeHandle { }
    public struct RuntimeFieldHandle { }
    public struct RuntimeMethodHandle { }

    public sealed class String
    {
        public static readonly string Empty = "";
        public int Length => 0;
        public char this[int index] => default;
        public static string Concat(object a, object b) => null;
        public static string Concat(object a, object b, object c) => null;
        public static string Concat(string a, string b) => null;
        public static string Concat(string a, string b, string c) => null;
        public static string Concat(string a, string b, string c, string d) => null;
        public static string Concat(params string[] values) => null;
        public static string Concat(params object[] values) => null;
        public static string Format(string format, object arg0) => null;
        public static string Format(string format, object arg0, object arg1) => null;
        public static string Format(string format, object arg0, object arg1, object arg2) => null;
        public static string Format(string format, params object[] args) => null;
        public static bool IsNullOrEmpty(string value) => false;
        public static bool operator ==(string a, string b) => false;
        public static bool operator !=(string a, string b) => false;
        public override bool Equals(object obj) => false;
        public override int GetHashCode() => 0;
    }

    public abstract class Array
    {
        public int Length => 0;
    }

    public abstract class Type
    {
        public static Type GetTypeFromHandle(RuntimeTypeHandle handle) => null;
        public abstract string Name { get; }
    }

    public abstract class Delegate { }
    public abstract class MulticastDelegate : Delegate { }
    public delegate void Action();
    public delegate void Action<in T>(T arg);
    public delegate TResult Func<out TResult>();
    public delegate TResult Func<in T, out TResult>(T arg);

    public class Exception
    {
        public Exception() { }
        public Exception(string message) { }
        public virtual string Message => null;
    }

    public class SystemException : Exception
    {
        public SystemException() { }
        public SystemException(string message) { }
    }

    public class InvalidOperationException : SystemException
    {
        public InvalidOperationException() { }
        public InvalidOperationException(string message) { }
    }

    public class ArgumentException : SystemException
    {
        public ArgumentException() { }
        public ArgumentException(string message) { }
    }

    public class NotSupportedException : SystemException
    {
        public NotSupportedException() { }
        public NotSupportedException(string message) { }
    }

    public class NullReferenceException : SystemException
    {
        public NullReferenceException() { }
        public NullReferenceException(string message) { }
    }

    public static class GC
    {
        public static void Collect() { }
        public static void WaitForPendingFinalizers() { }
        public static void SuppressFinalize(object obj) { }
    }

    public static class Activator
    {
        public static T CreateInstance<T>() => default;
    }

    public interface IDisposable
    {
        void Dispose();
    }

    public interface IComparable<in T>
    {
        int CompareTo(T other);
    }

    public interface IEquatable<T>
    {
        bool Equals(T other);
    }

    public abstract class Attribute { }

    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class AttributeUsageAttribute : Attribute
    {
        public AttributeUsageAttribute(AttributeTargets validOn) { }
        public bool AllowMultiple { get; set; }
        public bool Inherited { get; set; }
    }

    [Flags]
    public enum AttributeTargets
    {
        Assembly = 1, Module = 2, Class = 4, Struct = 8, Enum = 16, Constructor = 32, Method = 64, Property = 128,
        Field = 256, Event = 512, Interface = 1024, Parameter = 2048, Delegate = 4096, ReturnValue = 8192,
        GenericParameter = 16384, All = 32767,
    }

    [AttributeUsage(AttributeTargets.Enum)]
    public sealed class FlagsAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ParamArrayAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.All)]
    public sealed class SerializableAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NonSerializedAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.All)]
    public sealed class ObsoleteAttribute : Attribute
    {
        public ObsoleteAttribute() { }
        public ObsoleteAttribute(string message) { }
        public ObsoleteAttribute(string message, bool error) { }
    }

    [AttributeUsage(AttributeTargets.All)]
    public sealed class CLSCompliantAttribute : Attribute
    {
        public CLSCompliantAttribute(bool isCompliant) { }
    }

    public static class Math
    {
        public static float Abs(float value) => value;
        public static int Abs(int value) => value;
        public static double Sqrt(double d) => d;
        public static float Max(float a, float b) => a;
        public static float Min(float a, float b) => a;
        public static int Max(int a, int b) => a;
        public static int Min(int a, int b) => a;
    }

    public static class Console
    {
        public static void WriteLine(string value) { }
    }
}

namespace System.Collections
{
    public interface IEnumerable
    {
        IEnumerator GetEnumerator();
    }

    public interface IEnumerator
    {
        bool MoveNext();
        object Current { get; }
        void Reset();
    }
}

namespace System.Collections.Generic
{
    public interface IEnumerable<out T> : IEnumerable
    {
        new IEnumerator<T> GetEnumerator();
    }

    public interface IEnumerator<out T> : IEnumerator, IDisposable
    {
        new T Current { get; }
    }

    public interface ICollection<T> : IEnumerable<T>
    {
        int Count { get; }
        void Add(T item);
    }

    public interface IList<T> : ICollection<T>
    {
        T this[int index] { get; set; }
    }

    public interface IReadOnlyCollection<out T> : IEnumerable<T>
    {
        int Count { get; }
    }

    public interface IReadOnlyList<out T> : IReadOnlyCollection<T>
    {
        T this[int index] { get; }
    }

    public class List<T> : IList<T>, IReadOnlyList<T>
    {
        public List() { }
        public List(IEnumerable<T> collection) { }
        public int Count => 0;
        public T this[int index] { get => default; set { } }
        public void Add(T item) { }
        public void RemoveAt(int index) { }
        public void Clear() { }
        public bool Contains(T item) => false;
        public Enumerator GetEnumerator() => default;
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => null;
        IEnumerator IEnumerable.GetEnumerator() => null;

        public struct Enumerator : IEnumerator<T>
        {
            public T Current => default;
            object IEnumerator.Current => null;
            public bool MoveNext() => false;
            public void Reset() { }
            public void Dispose() { }
        }
    }

    public class Dictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        public Dictionary() { }
        public KeyCollection Keys => null;
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => null;
        IEnumerator IEnumerable.GetEnumerator() => null;

        public sealed class KeyCollection : IEnumerable<TKey>
        {
            public int Count => 0;
            public IEnumerator<TKey> GetEnumerator() => null;
            IEnumerator IEnumerable.GetEnumerator() => null;
        }
        public int Count => 0;
        public TValue this[TKey key] { get => default; set { } }
        public void Add(TKey key, TValue value) { }
        public bool ContainsKey(TKey key) => false;
        public bool Remove(TKey key) => false;
        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default;
            return false;
        }
    }
}

namespace System.Collections.Generic
{
    public struct KeyValuePair<TKey, TValue>
    {
        public TKey Key => default;
        public TValue Value => default;
    }
}

namespace System.Globalization
{
    public class CultureInfo : IFormatProvider
    {
        public static CultureInfo InvariantCulture => null;
        public static CultureInfo CurrentCulture => null;
        public object GetFormat(Type formatType) => null;
    }
}

namespace System.Reflection
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
    public sealed class DefaultMemberAttribute : Attribute
    {
        public DefaultMemberAttribute(string memberName) { }
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class AssemblyVersionAttribute : Attribute
    {
        public AssemblyVersionAttribute(string version) { }
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.All)]
    public sealed class CompilerGeneratedAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Module | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Interface | AttributeTargets.Delegate)]
    public sealed class CompilationRelaxationsAttribute : Attribute
    {
        public CompilationRelaxationsAttribute(int relaxations) { }
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class RuntimeCompatibilityAttribute : Attribute
    {
        public bool WrapNonExceptionThrows { get; set; }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class TypeForwardedToAttribute : Attribute
    {
        public TypeForwardedToAttribute(Type destination) { }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
    public sealed class ExtensionAttribute : Attribute { }

    public static class RuntimeHelpers
    {
        public static void InitializeArray(Array array, RuntimeFieldHandle fldHandle) { }
        public static int OffsetToStringData => 0;
    }
}

namespace System.Diagnostics
{
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Module)]
    public sealed class DebuggableAttribute : Attribute
    {
        public DebuggableAttribute(DebuggingModes modes) { }

        [Flags]
        public enum DebuggingModes
        {
            None = 0, Default = 1, IgnoreSymbolStoreSequencePoints = 2, EnableEditAndContinue = 4, DisableOptimizations = 256,
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class ConditionalAttribute : Attribute
    {
        public ConditionalAttribute(string conditionString) { }
    }
}

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class StructLayoutAttribute : Attribute
    {
        public StructLayoutAttribute(LayoutKind layoutKind) { }
    }

    public enum LayoutKind
    {
        Sequential = 0, Explicit = 2, Auto = 3,
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DllImportAttribute : Attribute
    {
        public DllImportAttribute(string dllName) { }
        public string EntryPoint;
    }
}

namespace System.IO
{
    public static class File
    {
        public static string ReadAllText(string path) => null;
    }
}
