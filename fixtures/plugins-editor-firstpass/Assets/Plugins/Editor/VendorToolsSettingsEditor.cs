using UnityEditor;

[CustomEditor(typeof(VendorToolsSettings))]
public class VendorToolsSettingsEditor : Editor
{
    public static bool ShowAdvanced;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Ask your account manager for an API key.", MessageType.Info);
    }
}
