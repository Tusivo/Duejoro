// SPDX-License-Identifier: AGPL-3.0-or-later
namespace Duejoro.Core;

/// <summary>
/// Where Duejoro keeps its own files (the records, attached documents, settings and backups). Like Ledjoro and Pagjoro, it uses a folder called
/// <c>Duejoro-Data</c> next to <c>Duejoro.exe</c>, so the program and its data can be copied together. When that folder cannot be written
/// (a program installed in a protected folder, a read-only drive) the person's own application-data folder is used instead.
/// </summary>
public static class AppPaths
{
    public const string FolderName = "Duejoro-Data";

    static string? resolved;

    /// <summary>The folder Duejoro keeps its files in. It exists and is writable when this returns.</summary>
    public static string DataFolder => resolved ??= Resolve();

    /// <summary>True when <see cref="DataFolder"/> is the one beside the program.</summary>
    public static bool IsPortable => string.Equals(Path.GetFileName(DataFolder), FolderName, StringComparison.OrdinalIgnoreCase);

    /// <summary>For tests: choose the folder directly.</summary>
    internal static void UseFolder(string? folder) => resolved = folder;

    /// <summary>Used when the folder beside the program cannot be written.</summary>
    public static string FallbackFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Duejoro");

    static string Resolve()
    {
        var exe = Environment.ProcessPath;
        // only the program itself uses a folder beside it; a test run or a development tool must not scatter folders next to dotnet.exe
        if (exe is not null && string.Equals(Path.GetFileNameWithoutExtension(exe), "Duejoro", StringComparison.OrdinalIgnoreCase))
        {
            var beside = Path.Combine(Path.GetDirectoryName(exe)!, FolderName);
            if (TryPrepare(beside)) return beside;
        }
        var fallback = FallbackFolder;
        TryPrepare(fallback);
        return fallback;
    }

    static bool TryPrepare(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, ".write-test-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return false; }
    }
}
