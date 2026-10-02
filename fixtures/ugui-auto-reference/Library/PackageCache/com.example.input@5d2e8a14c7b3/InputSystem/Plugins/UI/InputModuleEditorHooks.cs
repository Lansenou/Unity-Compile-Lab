// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.

#if UNITY_EDITOR
namespace Example.Input.UI
{
    // In the Editor, Unity also auto-references UnityEditor.UI, even from a runtime assembly.
    public static class InputModuleEditorHooks
    {
        public static System.Type EditorType => typeof(UnityEditor.UI.SelectableEditor);
    }
}
#endif
