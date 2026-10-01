// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor
{
    /// <summary>Base class for custom inspectors.</summary>
    public class Editor : ScriptableObject
    {
        public Object target { get => throw null; set => throw null; }

        public Object[] targets => throw null;

        public SerializedObject serializedObject => throw null;

        public virtual void OnInspectorGUI() => throw null;

        public bool DrawDefaultInspector() => throw null;

        public void Repaint() => throw null;
    }

    /// <summary>Base class for editor windows.</summary>
    public class EditorWindow : ScriptableObject
    {
        public string title { get => throw null; set => throw null; }

        public GUIContent titleContent { get => throw null; set => throw null; }

        public static T GetWindow<T>() where T : EditorWindow => throw null;

        public static T GetWindow<T>(string title) where T : EditorWindow => throw null;

        public void Show() => throw null;

        public void Close() => throw null;

        public void Repaint() => throw null;
    }

    /// <summary>Wraps a serialized Unity object for editing.</summary>
    public sealed class SerializedObject
    {
        public SerializedObject(Object obj) => throw null;

        public SerializedProperty FindProperty(string propertyPath) => throw null;

        public void Update() => throw null;

        public bool ApplyModifiedProperties() => throw null;
    }

    /// <summary>A property of a SerializedObject.</summary>
    public sealed class SerializedProperty
    {
        private SerializedProperty()
        {
        }

        public string name => throw null;

        public float floatValue { get => throw null; set => throw null; }

        public int intValue { get => throw null; set => throw null; }

        public bool boolValue { get => throw null; set => throw null; }

        public string stringValue { get => throw null; set => throw null; }
    }

    /// <summary>Adds a static method to the main menu or a context menu.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class MenuItem : Attribute
    {
        public string menuItem;
        public bool validate;
        public int priority;

        public MenuItem(string itemName) => throw null;

        public MenuItem(string itemName, bool isValidateFunction) => throw null;

        public MenuItem(string itemName, bool isValidateFunction, int priority) => throw null;
    }

    /// <summary>Declares the type a custom Editor is for.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class CustomEditor : Attribute
    {
        public bool isFallback;

        public CustomEditor(Type inspectedType) => throw null;

        public CustomEditor(Type inspectedType, bool editorForChildClasses) => throw null;
    }

    /// <summary>Access to assets in the project.</summary>
    public static class AssetDatabase
    {
        public static string[] FindAssets(string filter) => throw null;

        public static string[] FindAssets(string filter, string[] searchInFolders) => throw null;

        public static string GUIDToAssetPath(string guid) => throw null;

        public static string AssetPathToGUID(string path) => throw null;

        public static string GetAssetPath(Object assetObject) => throw null;

        public static T LoadAssetAtPath<T>(string assetPath) where T : Object => throw null;

        public static void CreateAsset(Object asset, string path) => throw null;

        public static void SaveAssets() => throw null;

        public static void Refresh() => throw null;
    }

    /// <summary>Auto-layout controls for editor GUI.</summary>
    public static class EditorGUILayout
    {
        public static void LabelField(string label) => throw null;

        public static void LabelField(string label, string label2) => throw null;

        public static float FloatField(string label, float value) => throw null;

        public static int IntField(string label, int value) => throw null;

        public static string TextField(string label, string text) => throw null;

        public static bool Toggle(string label, bool value) => throw null;

        public static Object ObjectField(string label, Object obj, Type objType, bool allowSceneObjects) => throw null;

        public static bool PropertyField(SerializedProperty property) => throw null;

        public static void HelpBox(string message, MessageType type) => throw null;

        public static void Space() => throw null;
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
        public static void SetDirty(Object target) => throw null;

        public static bool DisplayDialog(string title, string message, string ok) => throw null;

        public static bool DisplayDialog(string title, string message, string ok, string cancel) => throw null;
    }

    /// <summary>Editor application state.</summary>
    public static class EditorApplication
    {
        public static bool isPlaying { get => throw null; set => throw null; }

        public static bool isCompiling => throw null;
    }
}
