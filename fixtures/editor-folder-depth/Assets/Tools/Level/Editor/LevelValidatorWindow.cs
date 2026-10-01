using UnityEditor;
using UnityEngine;

public class LevelValidatorWindow : EditorWindow
{
    [MenuItem("Tools/Level/Validate")]
    private static void Open()
    {
        GetWindow<LevelValidatorWindow>("Level Validator");
    }

    private void OnGUI()
    {
        var level = FindAnyObjectByType<LevelInfo>();
        if (level == null)
        {
            EditorGUILayout.HelpBox("No LevelInfo in the open scene.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Level", level.DisplayName);
        if (GUILayout.Button("Validate"))
        {
            Debug.Log(level.Validate() ? "Level is valid" : "Level has problems", level);
        }
    }
}
