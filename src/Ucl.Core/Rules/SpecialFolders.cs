namespace Ucl.Core.Rules;

/// <summary>Unity's special-folder rules for scripts that are not under an asmdef or asmref.</summary>
public static class SpecialFolders
{
    /// <summary>Runtime first-pass assembly name.</summary>
    public const string FirstPass = "Assembly-CSharp-firstpass";

    /// <summary>Main runtime assembly name.</summary>
    public const string Main = "Assembly-CSharp";

    /// <summary>Editor first-pass assembly name.</summary>
    public const string EditorFirstPass = "Assembly-CSharp-Editor-firstpass";

    /// <summary>Main Editor assembly name.</summary>
    public const string Editor = "Assembly-CSharp-Editor";

    /// <summary>Predefined assemblies in compile order.</summary>
    public static IReadOnlyList<string> Predefined { get; } = [FirstPass, Main, EditorFirstPass, Editor];

    private static readonly string[] FirstPassRoots = ["Assets/Plugins/", "Assets/Standard Assets/", "Assets/Pro Standard Assets/"];

    /// <summary>True when the path has a folder named <c>Editor</c> (any depth, case-insensitive) below <c>Assets</c>.</summary>
    public static bool IsInEditorFolder(string path)
    {
        var parts = path.Split('/');
        for (var i = 1; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("Editor", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True for scripts under <c>Assets/Plugins</c>, <c>Assets/Standard Assets</c> or <c>Assets/Pro Standard Assets</c>.</summary>
    public static bool IsFirstPass(string path) =>
        FirstPassRoots.Any(r => path.StartsWith(r, StringComparison.OrdinalIgnoreCase));

    /// <summary>The predefined assembly a script under <c>Assets/</c> belongs to.</summary>
    public static string PredefinedAssemblyFor(string path) => (IsFirstPass(path), IsInEditorFolder(path)) switch
    {
        (true, true) => EditorFirstPass,
        (true, false) => FirstPass,
        (false, true) => Editor,
        _ => Main,
    };

    /// <summary>True for the two Editor predefined assemblies.</summary>
    public static bool IsEditorPredefined(string name) => name is Editor or EditorFirstPass;
}
