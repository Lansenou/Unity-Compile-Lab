using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ucl.Discovery;

namespace Ucl.Cli;

/// <summary>Builds a private scratch player and publishes immutable, integrity-checked cache entries.</summary>
internal static class ProjectPlayerCache
{
    public static string Get(Session session, EditorInstall editor, TextWriter progress)
    {
        Validate(session, editor);
        return GetSupported(session, editor, progress);
    }

    internal static void Validate(Session session, EditorInstall editor)
    {
        if (!OperatingSystem.IsWindows() || editor.Version.ToString() != "6000.3.19f1")
            throw new ArgumentException("--host supports Windows Mono with Unity 6000.3.19f1 only; use the Editor for other versions/platforms.");
        var utfRoot = session.Project.PackageRoots.GetValueOrDefault("Packages/com.unity.test-framework");
        if (utfRoot is null || JsonDocument.Parse(File.ReadAllText(Path.Combine(utfRoot, "package.json"))).RootElement.GetProperty("version").GetString() != "1.6.0")
            throw new ArgumentException("--host requires Unity Test Framework 1.6.0; its internal player runner is version-pinned.");
    }

    private static string GetSupported(Session session, EditorInstall editor, TextWriter progress)
    {
        var cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ucl", "player-hosts", "v1");
        Directory.CreateDirectory(cacheRoot);
        var key = Key(session, editor);
        var entry = Path.Combine(cacheRoot, key);
        var playerDirectory = Path.Combine(entry, "player");
        if (PlayerHostCache.Valid(playerDirectory, key))
        {
            progress.WriteLine("host cache: validated warm entry " + key);
            return Path.Combine(playerDirectory, "Host.exe");
        }
        // A per-key file lock prevents duplicate builds. Other keys and player runs remain independent.
        using var fileLock = Lock(Path.Combine(cacheRoot, key + ".lock"));
        if (PlayerHostCache.Valid(playerDirectory, key)) return Path.Combine(playerDirectory, "Host.exe");
        var buildRoot = Path.Combine(cacheRoot, "build-" + Guid.NewGuid().ToString("N"));
        var scratch = ScratchRoot(buildRoot, session.ProjectRoot);
        var buildPlayer = Path.Combine(buildRoot, "player");
        Directory.CreateDirectory(scratch);
        progress.WriteLine("host cache: cold build, log " + Path.Combine(buildRoot, "build.log"));
        foreach (var directory in new[] { "Assets", "Packages", "ProjectSettings" })
            CopyTree(Path.Combine(session.ProjectRoot, directory), Path.Combine(scratch, directory));
        CopyTree(Path.Combine(session.ProjectRoot, "Library", "PackageCache"), Path.Combine(scratch, "Library", "PackageCache"));
        foreach (var (name, root) in session.Project.PackageRoots)
        {
            // Local file packages must not keep a reference to input project files in the scratch manifest.
            if (root.StartsWith(Path.Combine(session.ProjectRoot, "Packages") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            if (root.StartsWith(Path.Combine(session.ProjectRoot, "Library") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            if (root.StartsWith(editor.DataPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            CopyTree(root, Path.Combine(scratch, name));
        }
        var sourceDirectory = new DirectoryInfo(session.ProjectRoot);
        var targetDirectory = new DirectoryInfo(scratch);
        while (sourceDirectory is not null)
        {
            Directory.CreateDirectory(targetDirectory.FullName);
            foreach (var config in sourceDirectory.EnumerateFiles().Where(f => f.Name == ".editorconfig" || f.Name.EndsWith(".globalconfig", StringComparison.Ordinal)))
                File.Copy(config.FullName, Path.Combine(targetDirectory.FullName, config.Name), overwrite: true);
            sourceDirectory = sourceDirectory.Parent;
            targetDirectory = targetDirectory.Parent!;
        }
        var bootstrap = Path.Combine(scratch, "Assets", "UclProjectHost");
        if (Directory.Exists(bootstrap)) throw new ArgumentException("Assets/UclProjectHost is reserved for the scratch host bootstrap; input project conflicts.");
        Directory.CreateDirectory(Path.Combine(bootstrap, "Editor"));
        File.WriteAllText(Path.Combine(bootstrap, "ProjectTestHost.cs"), Source("ProjectTestHost.cs"));
        File.WriteAllText(Path.Combine(bootstrap, "Editor", "ProjectHostBuild.cs"), Source("ProjectHostBuild.cs"));
        File.WriteAllText(Path.Combine(bootstrap, "Ucl.ProjectHost.asmdef"), "{\"name\":\"Ucl.ProjectHost\",\"references\":[\"UnityEngine.TestRunner\"],\"overrideReferences\":true,\"precompiledReferences\":[\"nunit.framework.dll\"]}");
        File.WriteAllText(Path.Combine(bootstrap, "Editor", "Ucl.ProjectHost.Editor.asmdef"), "{\"name\":\"Ucl.ProjectHost.Editor\",\"references\":[],\"includePlatforms\":[\"Editor\"]}");
        Directory.CreateDirectory(buildPlayer);
        var executable = Path.Combine(editor.DataPath, "..", "Unity.exe");
        Run(executable, ["-batchmode", "-nographics", "-quit", "-projectPath", scratch, "-executeMethod", "ProjectHostBuild.Build",
            "-playerOutput", Path.Combine(buildPlayer, "Host.exe"), "-logFile", Path.Combine(buildRoot, "build.log")], buildRoot, TimeSpan.FromMinutes(30));
        if (!File.Exists(Path.Combine(buildPlayer, "Host.exe"))) throw new IOException("Host build completed without a player; see " + Path.Combine(buildRoot, "build.log"));
        if (Key(session, editor) != key) throw new IOException("Project inputs changed during the host build; refusing to publish a stale player.");
        PlayerHostCache.Seal(buildPlayer, key);
        if (!PlayerHostCache.Valid(buildPlayer, key)) throw new IOException("Built player integrity check failed.");
        Directory.CreateDirectory(entry);
        if (Directory.Exists(playerDirectory))
            Directory.Move(playerDirectory, Path.Combine(entry, "invalid-" + Guid.NewGuid().ToString("N")));
        Directory.Move(buildPlayer, playerDirectory);
        return Path.Combine(playerDirectory, "Host.exe");
    }

    // Preserve directory names as well as depth: inherited editorconfig globs are relative to each config.
    internal static string ScratchRoot(string buildRoot, string inputRoot)
    {
        var names = new Stack<string>();
        for (var directory = new DirectoryInfo(inputRoot); directory.Parent is not null; directory = directory.Parent)
            names.Push(directory.Name);
        return names.Aggregate(Path.Combine(buildRoot, "project"), (path, name) => Path.Combine(path, name));
    }

    internal static FileStream Lock(string path)
    {
        var end = DateTime.UtcNow + TimeSpan.FromMinutes(30);
        while (true)
        {
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (DateTime.UtcNow < end) { Thread.Sleep(1000); }
        }
    }

    private static string Key(Session session, EditorInstall editor)
    {
        var inputs = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = editor.Version.ToString(),
            ["scratchLayout"] = ScratchRoot("", session.ProjectRoot).Replace('\\', '/'),
            ["editorBinary"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(editor.DataPath, "..", "Unity.exe")))),
            ["protocol"] = "ucl-project-host/3 StandaloneWindows64 Development Mono stripping-disabled IncludeTestAssemblies",
            ["runner"] = Source("ProjectTestHost.cs"),
            ["builder"] = Source("ProjectHostBuild.cs"),
        };
        var roots = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["editorManaged"] = Path.Combine(editor.DataPath, "Managed"),
        };
        foreach (var directory in new[] { "Assets", "Packages", "ProjectSettings", "Library/PackageCache" })
            roots[directory] = Path.Combine(session.ProjectRoot, directory);
        foreach (var (name, root) in session.Project.PackageRoots.OrderBy(p => p.Key, StringComparer.Ordinal))
            roots["resolved:" + name] = root;
        var digests = PlayerHostCache.TreeDigests(roots.Values.ToArray());
        foreach (var (name, root) in roots) inputs[name] = digests[root];
        var level = 0;
        for (var directory = new DirectoryInfo(session.ProjectRoot); directory is not null; directory = directory.Parent)
        {
            foreach (var config in directory.EnumerateFiles().Where(f => f.Name == ".editorconfig" || f.Name.EndsWith(".globalconfig", StringComparison.Ordinal)))
                inputs[$"config:{level}:{config.Name}"] = File.ReadAllText(config.FullName);
            level++;
        }
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(inputs))));
    }

    private static string Source(string name)
    {
        using var stream = typeof(ProjectPlayerCache).Assembly.GetManifestResourceStream("Ucl.Cli.PlayerHost." + name)
            ?? throw new InvalidOperationException("Missing embedded player host source " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void CopyTree(string source, string target)
    {
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(target);
        foreach (var path in Directory.EnumerateFiles(source)) File.Copy(path, Path.Combine(target, Path.GetFileName(path)), overwrite: true);
        foreach (var path in Directory.EnumerateDirectories(source)) CopyTree(path, Path.Combine(target, Path.GetFileName(path)));
    }

    internal static ProcessResult Run(string program, IReadOnlyList<string> arguments, string directory, TimeSpan timeout, bool allowTestFailure = false)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        var result = new SystemProcessRunner().RunAsync(program, arguments, directory, cancellation.Token).GetAwaiter().GetResult();
        if (!result.Started || result.ExitCode != 0 && !allowTestFailure)
            throw new IOException($"Host process failed ({result.ExitCode}): {result.Stderr}");
        return result;
    }
}
