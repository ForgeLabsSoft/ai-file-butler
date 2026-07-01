namespace AIFileButler;

/// <summary>
/// "What's eating my disk" — scans a folder and returns its biggest files plus which
/// immediate sub-folder is largest. Read-only; the caller decides what (if anything)
/// to delete. Skips cloud placeholders and links. Adapted from the DriveForge analyzer.
/// </summary>
internal static class DiskUsage
{
    public sealed record BigFile(string Path, long Size);
    public sealed record Result(List<BigFile> Largest, long TotalBytes, int FileCount, string BiggestFolder, long BiggestFolderBytes);

    public static Result Scan(string root, int topN = 300, Func<bool>? cancelled = null)
    {
        var all = new List<BigFile>();
        long total = 0; int count = 0;
        var folderSize = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in Enumerate(root))
        {
            if (cancelled?.Invoke() == true) break;
            long size; try { size = new FileInfo(f).Length; } catch { continue; }
            total += size; count++;
            all.Add(new BigFile(f, size));
            var sub = ImmediateSub(root, f);
            folderSize[sub] = folderSize.GetValueOrDefault(sub) + size;
        }

        var largest = all.OrderByDescending(x => x.Size).Take(topN).ToList();
        string biggest = ""; long biggestBytes = 0;
        foreach (var kv in folderSize)
            if (kv.Value > biggestBytes) { biggest = kv.Key; biggestBytes = kv.Value; }
        return new Result(largest, total, count, biggest, biggestBytes);
    }

    // The top-level sub-folder of `root` that contains `file` (or root's own name if the
    // file sits directly in root).
    private static string ImmediateSub(string root, string file)
    {
        try
        {
            root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            var rel = Path.GetFullPath(file)[(root.Length + 1)..];
            int slash = rel.IndexOf(Path.DirectorySeparatorChar);
            return slash < 0 ? Path.GetFileName(root) : Path.Combine(root, rel[..slash]);
        }
        catch { return root; }
    }

    private static IEnumerable<string> Enumerate(string root)
    {
        if (!Directory.Exists(root)) yield break;
        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories); }
        catch { yield break; }
        foreach (var f in files)
        {
            FileAttributes a;
            try { a = new FileInfo(f).Attributes; } catch { continue; }
            const FileAttributes RecallOnAccess = (FileAttributes)0x400000;
            if ((a & (FileAttributes.Offline | RecallOnAccess | FileAttributes.ReparsePoint)) != 0) continue;
            yield return f;
        }
    }
}
