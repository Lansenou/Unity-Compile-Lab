// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

using System;

namespace UnityEngine
{
    /// <summary>Serializes a private field.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class SerializeField : Attribute
    {
    }

    /// <summary>Hides a field in the Inspector.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class HideInInspector : Attribute
    {
    }

    /// <summary>Adds required components automatically.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireComponent : Attribute
    {
        public Type m_Type0;
        public Type m_Type1;
        public Type m_Type2;

        public RequireComponent(Type requiredComponent) => throw null;

        public RequireComponent(Type requiredComponent, Type requiredComponent2) => throw null;

        public RequireComponent(Type requiredComponent, Type requiredComponent2, Type requiredComponent3) => throw null;
    }

    /// <summary>Prevents adding a component twice to one GameObject.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class DisallowMultipleComponent : Attribute
    {
    }

    /// <summary>Places the component in the Add Component menu.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class AddComponentMenu : Attribute
    {
        public AddComponentMenu(string menuName) => throw null;
    }

    /// <summary>Base class of property attributes.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public abstract class PropertyAttribute : Attribute
    {
        public int order { get => throw null; set => throw null; }
    }

    /// <summary>Adds a header above fields in the Inspector.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = true)]
    public class HeaderAttribute : PropertyAttribute
    {
        public readonly string header;

        public HeaderAttribute(string header) => throw null;
    }

    /// <summary>Shows a tooltip for a field in the Inspector.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class TooltipAttribute : PropertyAttribute
    {
        public readonly string tooltip;

        public TooltipAttribute(string tooltip) => throw null;
    }

    /// <summary>Restricts a numeric field to a range.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class RangeAttribute : PropertyAttribute
    {
        public readonly float min;
        public readonly float max;

        public RangeAttribute(float min, float max) => throw null;
    }

    /// <summary>Lists a ScriptableObject type in the Assets/Create menu.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string menuName { get => throw null; set => throw null; }

        public string fileName { get => throw null; set => throw null; }

        public int order { get => throw null; set => throw null; }
    }

    /// <summary>Runs a static method when the runtime loads.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() => throw null;

        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) => throw null;
    }

    /// <summary>When a RuntimeInitializeOnLoadMethod runs.</summary>
    public enum RuntimeInitializeLoadType
    {
        AfterSceneLoad = 0,
        BeforeSceneLoad = 1,
        AfterAssembliesLoaded = 2,
        BeforeSplashScreen = 3,
        SubsystemRegistration = 4,
    }
}
