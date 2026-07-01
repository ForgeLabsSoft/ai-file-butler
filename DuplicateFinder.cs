using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using System.Security.Cryptography;

namespace AIFileButler;

/// <summary>
/// Finds duplicate files under a folder. Two modes:
///  • Exact  — byte-identical: bucket by size, then a head+tail partial hash, then a
///             full SHA-256 (hashing in parallel). Fast and collision-safe.
///  • Similar — the same PHOTO even after resize / recompress / re-save, via a 64-bit
///             perceptual difference hash (dHash) clustered by Hamming distance.
/// Skips empty files, cloud placeholders and links. The caller decides what to delete
/// (always to the Recycle Bin, always keeping at least one copy).
/// Technique adapted from the ForgeLabs DriveForge disk-space analyzer.
/// </summary>
internal static class DuplicateFinder
{
    public sealed record FileRef(string Path, long Size);

    public sealed class DupGroup
    {
        public bool Exact = true;                 // true = byte-identical, false = visually similar
        public List<FileRef> Files = new();
        public long Size => Files.Count > 0 ? Files[0].Size : 0;
        public long Wasted => Files.Count < 2 ? 0 : Files.OrderByDescending(f => f.Size).Skip(1).Sum(f => f.Size);
    }

    private static readonly HashSet<string> ImgExt = new(StringComparer.OrdinalIgnoreCase)
    { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic", ".heif" };

    private static ParallelOptions Par(Func<bool>? cancelled) =>
        new() { MaxDegreeOfParallelism = Math.Max(2, Math.Min(Environment.ProcessorCount, 8)) };

    // ---- exact (byte-identical) duplicates -------------------------------

    public static List<DupGroup> FindExact(string root, Action<int, int>? progress = null, Func<bool>? cancelled = null)
    {
        var groups = new List<DupGroup>();
        if (!Directory.Exists(root)) return groups;

        var bySize = new Dictionary<long, List<string>>();
        foreach (var f in Enumerate(root))
        {
            if (cancelled?.Invoke() == true) return groups;
            long size; try { size = new FileInfo(f).Length; } catch { continue; }
            if (size == 0) continue;
            if (!bySize.TryGetValue(size, out var l)) bySize[size] = l = new();
            l.Add(f);
        }

        var candidates = bySize.Where(kv => kv.Value.Count > 1).ToList();
        int total = candidates.Sum(c => c.Value.Count), done = 0;

        foreach (var kv in candidates)
        {
            if (cancelled?.Invoke() == true) return groups;
            // cheap head+tail pre-filter so unique files are never fully read
            var byPartial = new Dictionary<string, List<string>>();
            foreach (var f in kv.Value)
            {
                progress?.Invoke(done++, total);
                var ph = Hash(f, kv.Key, partial: true); if (ph is null) continue;
                if (!byPartial.TryGetValue(ph, out var l)) byPartial[ph] = l = new();
                l.Add(f);
            }
            foreach (var part in byPartial.Values.Where(v => v.Count > 1))
            {
                var full = new ConcurrentDictionary<string, List<string>>();
                Parallel.ForEach(part, Par(cancelled), f =>
                {
                    var fh = Hash(f, kv.Key, partial: false); if (fh is null) return;
                    full.AddOrUpdate(fh, _ => new List<string> { f }, (_, l) => { lock (l) l.Add(f); return l; });
                });
                foreach (var g in full.Values.Where(v => v.Count > 1))
                    groups.Add(new DupGroup { Exact = true, Files = g.OrderBy(p => p.Length).Select(p => new FileRef(p, kv.Key)).ToList() });
            }
        }

        progress?.Invoke(total, total);
        return groups.OrderByDescending(g => g.Wasted).ToList();
    }

    // ---- similar (near-duplicate) images ---------------------------------

    private const int SimImageCap = 8000; // keeps the O(n²) cluster pass fast

    public static List<DupGroup> FindSimilarImages(string root, int threshold, Action<int, int>? progress = null, Func<bool>? cancelled = null)
    {
        var groups = new List<DupGroup>();
        if (!Directory.Exists(root)) return groups;

        var imgs = new List<(string Path, long Size)>();
        foreach (var f in Enumerate(root))
        {
            if (cancelled?.Invoke() == true) return groups;
            if (!ImgExt.Contains(Path.GetExtension(f))) continue;
            long size; try { size = new FileInfo(f).Length; } catch { continue; }
            imgs.Add((f, size));
            if (imgs.Count >= SimImageCap) break;
        }

        var hashes = new ulong?[imgs.Count];
        int done = 0;
        Parallel.For(0, imgs.Count, Par(cancelled), i =>
        {
            if (cancelled?.Invoke() == true) return;
            hashes[i] = ComputeDHash(imgs[i].Path);
            int n = System.Threading.Interlocked.Increment(ref done);
            if ((n & 31) == 0) progress?.Invoke(n, imgs.Count);
        });

        // union-find: cluster images whose dHash is within `threshold` bits
        var valid = Enumerable.Range(0, imgs.Count).Where(i => hashes[i].HasValue).ToList();
        var parent = Enumerable.Range(0, imgs.Count).ToArray();
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(int a, int b) { int ra = Find(a), rb = Find(b); if (ra != rb) parent[ra] = rb; }
        for (int a = 0; a < valid.Count; a++)
        {
            if (cancelled?.Invoke() == true) break;
            ulong ha = hashes[valid[a]]!.Value;
            for (int b = a + 1; b < valid.Count; b++)
                if (BitOperations.PopCount(ha ^ hashes[valid[b]]!.Value) <= threshold) Union(valid[a], valid[b]);
        }

        var clusters = new Dictionary<int, List<int>>();
        foreach (int i in valid) { int r = Find(i); if (!clusters.TryGetValue(r, out var l)) clusters[r] = l = new(); l.Add(i); }
        foreach (var cl in clusters.Values.Where(v => v.Count >= 2))
            groups.Add(new DupGroup { Exact = false, Files = cl.Select(i => new FileRef(imgs[i].Path, imgs[i].Size)).OrderByDescending(f => f.Size).ToList() });

        progress?.Invoke(imgs.Count, imgs.Count);
        return groups.OrderByDescending(g => g.Files.Count).ToList();
    }

    // 64-bit difference hash: downscale to 9×8 grayscale, set a bit where a pixel is
    // brighter than its right neighbour. Robust to resize / recompression.
    private static ulong? ComputeDHash(string path)
    {
        try
        {
            using var bmp = new Bitmap(path);
            using var small = new Bitmap(9, 8, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(small))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(bmp, 0, 0, 9, 8);
            }
            ulong hash = 0; int bit = 0;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    var l = small.GetPixel(x, y); var r = small.GetPixel(x + 1, y);
                    double gl = 0.299 * l.R + 0.587 * l.G + 0.114 * l.B;
                    double gr = 0.299 * r.R + 0.587 * r.G + 0.114 * r.B;
                    if (gl > gr) hash |= 1UL << bit;
                    bit++;
                }
            return hash;
        }
        catch { return null; }
    }

    // ---- shared ----------------------------------------------------------

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

    private static string? Hash(string path, long size, bool partial)
    {
        try
        {
            const int CHUNK = 16384;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, CHUNK, FileOptions.SequentialScan);
            if (!partial)
            {
                using var sha = SHA256.Create();
                return Convert.ToHexString(sha.ComputeHash(fs));
            }
            using var inc = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            if (size <= CHUNK * 2L)
            {
                var all = new byte[size];
                fs.ReadExactly(all, 0, (int)size);
                inc.AppendData(all);
            }
            else
            {
                var head = new byte[CHUNK];
                fs.ReadExactly(head, 0, CHUNK);
                inc.AppendData(head);
                fs.Seek(-CHUNK, SeekOrigin.End);
                var tail = new byte[CHUNK];
                fs.ReadExactly(tail, 0, CHUNK);
                inc.AppendData(tail);
            }
            return Convert.ToHexString(inc.GetHashAndReset());
        }
        catch { return null; }
    }
}
