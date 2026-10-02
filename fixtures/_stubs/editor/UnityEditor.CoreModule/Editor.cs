// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor
{
    /// <summary>Base class for custom inspectors.</summary>
    public class Editor : ScriptableObject
    {
        public Object target { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public Object[] targets => throw Native.Unavailable();

        public SerializedObject serializedObject => throw Native.Unavailable();

        public virtual void OnInspectorGUI() => throw Native.Unavailable();

        public bool DrawDefaultInspector() => throw Native.Unavailable();

        public void Repaint() => throw Native.Unavailable();
    }

    /// <summary>Base class for editor windows.</summary>
    public class EditorWindow : ScriptableObject
    {
        public string title { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public GUIContent titleContent { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public static T GetWindow<T>() where T : EditorWindow => throw Native.Unavailable();

        public static T GetWindow<T>(string title) where T : EditorWindow => throw Native.Unavailable();

        public void Show() => throw Native.Unavailable();

        public void Close() => throw Native.Unavailable();

        public void Repaint() => throw Native.Unavailable();
    }

    /// <summary>Wraps a serialized Unity object for editing.</summary>
    public sealed class SerializedObject
    {
        public SerializedObject(Object obj) => throw Native.Unavailable();

        public SerializedProperty FindProperty(string propertyPath) => throw Native.Unavailable();

        public void Update() => throw Native.Unavailable();

        public bool ApplyModifiedProperties() => throw Native.Unavailable();
    }

    /// <summary>A property of a SerializedObject.</summary>
    public sealed class SerializedProperty
    {
        private SerializedProperty()
        {
        }

        public string name => throw Native.Unavailable();

        public float floatValue { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public int intValue { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public bool boolValue { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public string stringValue { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }
    }

    /// <summary>Adds a static method to the main menu or a context menu.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class MenuItem : Attribute
    {
        public string menuItem;
        public bool validate;
        public int priority;

        public MenuItem(string itemName) => throw Native.Unavailable();

        public MenuItem(string itemName, bool isValidateFunction) => throw Native.Unavailable();

        public MenuItem(string itemName, bool isValidateFunction, int priority) => throw Native.Unavailable();
    }

    /// <summary>Declares the type a custom Editor is for.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class CustomEditor : Attribute
    {
        public bool isFallback;

        public CustomEditor(Type inspectedType) => throw Native.Unavailable();

        public CustomEditor(Type inspectedType, bool editorForChildClasses) => throw Native.Unavailable();
    }

    /// <summary>Access to assets in the project.</summary>
    public static class AssetDatabase
    {
        public static string[] FindAssets(string filter) => throw Native.Unavailable();

        public static string[] FindAssets(string filter, string[] searchInFolders) => throw Native.Unavailable();

        public static string GUIDToAssetPath(string guid) => throw Native.Unavailable();

        public static string AssetPathToGUID(string path) => throw Native.Unavailable();

        public static string GetAssetPath(Object assetObject) => throw Native.Unavailable();

        public static T LoadAssetAtPath<T>(string assetPath) where T : Object => throw Native.Unavailable();

        public static void CreateAsset(Object asset, string path) => throw Native.Unavailable();

        public static void SaveAssets() => throw Native.Unavailable();

        public static void Refresh() => throw Native.Unavailable();
    }

    /// <summary>Auto-layout controls for editor GUI.</summary>
    public static class EditorGUILayout
    {
        public static void LabelField(string label) => throw Native.Unavailable();

        public static void LabelField(string label, string label2) => throw Native.Unavailable();

        public static float FloatField(string label, float value) => throw Native.Unavailable();

        public static int IntField(string label, int value) => throw Native.Unavailable();

        public static string TextField(string label, string text) => throw Native.Unavailable();

        public static bool Toggle(string label, bool value) => throw Native.Unavailable();

        public static Object ObjectField(string label, Object obj, Type objType, bool allowSceneObjects) => throw Native.Unavailable();

        public static bool PropertyField(SerializedProperty property) => throw Native.Unavailable();

        public static void HelpBox(string message, MessageType type) => throw Native.Unavailable();

        public static void Space() => throw Native.Unavailable();
    }

    /// <summary>Icon of a help box.</summary>
    public enum MessageType
    {
        None = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    /// <summary>Editor-wide utilities.</summary>
    public static class EditorUtility
    {
        public static void SetDirty(Object target) => throw Native.Unavailable();

        public static bool DisplayDialog(string title, string message, string ok) => throw Native.Unavailable();

        public static bool DisplayDialog(string title, string message, string ok, string cancel) => throw Native.Unavailable();
    }

    /// <summary>Editor application state.</summary>
    public static class EditorApplication
    {
        public static bool isPlaying { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public static bool isCompiling => throw Native.Unavailable();
    }
}
