using System;
using System.IO;

namespace AIFileButler;

/// <summary>
/// All mutable app state lives under %LOCALAPPDATA%\AI File Butler so the app works
/// even when installed read-only (Program Files, or a packaged Microsoft Store build)
/// — writing next to the .exe would throw there. Existing files written next to the
/// old single-file exe are migrated over on first access so nobody loses data.
/// </summary>
internal static class Paths
{
    public static readonly string Data = Init();

    private static string Init()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AI File Butler");
        try { Directory.CreateDirectory(dir); } catch { }
        return dir;
    }

    /// <summary>Absolute path to a state file in the data dir, migrating a same-named
    /// file left next to the .exe (older portable builds) on first use.</summary>
    public static string File(string name)
    {
        var dst = Path.Combine(Data, name);
        try
        {
            var legacy = Path.Combine(AppContext.BaseDirectory, name);
            if (!System.IO.File.Exists(dst) && System.IO.File.Exists(legacy) &&
                !string.Equals(Path.GetFullPath(legacy), Path.GetFullPath(dst), StringComparison.OrdinalIgnoreCase))
                System.IO.File.Copy(legacy, dst);
        }
        catch { /* best effort — a fresh file will just be created */ }
        return dst;
    }
}
