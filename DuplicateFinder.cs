using System.Security.Cryptography;

namespace AIFileButler;

/// <summary>
/// Finds byte-identical duplicate files under a folder. Fast and correct: bucket by
/// size first (different sizes can't be identical), then a cheap partial hash, then a
/// full SHA-256 only for the survivors. Empty files, cloud placeholders and links are
/// skipped. The caller decides what to delete (always to the Recycle Bin, keeping one).
/// </summary>
internal static class DuplicateFinder
{
    public sealed class DupGroup
    {
        public long Size;
        public List<string> Paths = new();
        public long Wasted => Size * Math.Max(0, Paths.Count - 1); // reclaimable bytes
    }

    public static List<DupGroup> Find(string root, Action<int, int>? progress = null, Func<bool>? cancelled = null)
    {
        var groups = new List<DupGroup>();
        if (!Directory.Exists(root)) return groups;

        // 1) bucket by exact size
        var bySize = new Dictionary<long, List<string>>();
        foreach (var f in Enumerate(root))
        {
            if (cancelled?.Invoke() == true) return groups;
            long size; try { size = new FileInfo(f).Length; } catch { continue; }
            if (size == 0) continue; // ignore empty files
            if (!bySize.TryGetValue(size, out var l)) bySize[size] = l = new();
            l.Add(f);
        }

        var candidates = bySize.Where(kv => kv.Value.Count > 1).ToList();
        int total = candidates.Sum(c => c.Value.Count), done = 0;

        foreach (var kv in candidates)
        {
            if (cancelled?.Invoke() == true) return groups;
            // 2) partial-hash pre-filter (first 8 KB) to avoid full-hashing unique files
            var byPartial = new Dictionary<string, List<string>>();
            foreach (var f in kv.Value)
            {
                progress?.Invoke(done++, total);
                var ph = Hash(f, partial: true); if (ph is null) continue;
                if (!byPartial.TryGetValue(ph, out var l)) byPartial[ph] = l = new();
                l.Add(f);
            }
            // 3) confirm with a full hash
            foreach (var part in byPartial.Values.Where(v => v.Count > 1))
            {
                var byFull = new Dictionary<string, List<string>>();
                foreach (var f in part)
                {
                    var fh = Hash(f, partial: false); if (fh is null) continue;
                    if (!byFull.TryGetValue(fh, out var l)) byFull[fh] = l = new();
                    l.Add(f);
                }
                foreach (var g in byFull.Values.Where(v => v.Count > 1))
                    groups.Add(new DupGroup { Size = kv.Key, Paths = g.OrderBy(p => p.Length).ToList() });
            }
        }

        progress?.Invoke(total, total);
        return groups.OrderByDescending(g => g.Wasted).ToList(); // biggest savings first
    }

    private static IEnumerable<string> Enumerate(string root)
    {
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

    private static string? Hash(string path, bool partial)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var sha = SHA256.Create();
            if (!partial) return Convert.ToHexString(sha.ComputeHash(fs));
            var buf = new byte[8192];
            int n = fs.Read(buf, 0, buf.Length);
            return Convert.ToHexString(sha.ComputeHash(buf, 0, n));
        }
        catch { return null; }
    }
}
