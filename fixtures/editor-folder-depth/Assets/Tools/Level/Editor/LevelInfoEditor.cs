using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelInfo))]
public class LevelInfoEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("Open Validator"))
        {
            EditorWindow.GetWindow<LevelValidatorWindow>("Level Validator");
        }
    }
}
