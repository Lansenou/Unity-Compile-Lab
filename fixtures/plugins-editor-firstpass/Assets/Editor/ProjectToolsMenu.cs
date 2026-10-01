using UnityEditor;

public static class ProjectToolsMenu
{
    [MenuItem("Tools/Vendor/Toggle Advanced Settings")]
    private static void ToggleAdvanced()
    {
        VendorToolsSettingsEditor.ShowAdvanced = !VendorToolsSettingsEditor.ShowAdvanced;
    }
}
