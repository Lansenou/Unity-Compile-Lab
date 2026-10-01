namespace Ucl.Discovery;

/// <summary>Port to the disk. Every read and write of <c>ucl</c> goes through it, which is how the read-only contract is enforced.</summary>
public interface IFileSystem
{
    /// <summary>True when the file exists.</summary>
    bool FileExists(string path);

    /// <summary>True when the directory exists.</summary>
    bool DirectoryExists(string path);

    /// <summary>Reads a UTF-8 text file (BOM tolerated).</summary>
    string ReadAllText(string path);

    /// <summary>Reads a file's bytes.</summary>
    byte[] ReadAllBytes(string path);

    /// <summary>Reads at most the first <paramref name="maxBytes"/> bytes of a file (fewer when the file is shorter).</summary>
    byte[] ReadPrefix(string path, int maxBytes);

    /// <summary>Names (not paths) of the files directly in <paramref name="directory"/>, ordinal order.</summary>
    IReadOnlyList<string> ListFiles(string directory);

    /// <summary>Names (not paths) of the subdirectories directly in <paramref name="directory"/>, ordinal order.</summary>
    IReadOnlyList<string> ListDirectories(string directory);

    /// <summary>Size and last-write time (UTC ticks) of a file, used to memoise content hashes.</summary>
    (long Length, long LastWriteUtcTicks) GetStamp(string path);

    /// <summary>Writes a file, creating its folder. Refuses paths outside the writable roots.</summary>
    void WriteAllBytes(string path, byte[] bytes);

    /// <summary>Deletes a file if it exists. Refuses paths outside the writable roots.</summary>
    void DeleteFile(string path);
}
