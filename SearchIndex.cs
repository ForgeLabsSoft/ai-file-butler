using System.Text.Json;

namespace AIFileButler;

/// <summary>
/// A tiny on-device semantic index over the user's filed documents. Each entry is
/// {path, folder, snippet, embedding}. Search embeds the query and ranks entries by
/// cosine similarity (brute force — fine for a personal library of thousands). Stored
/// as JSON in the data dir; nothing leaves the device.
/// </summary>
internal static class SearchIndex
{
    public sealed class Entry
    {
        public string Path { get; set; } = "";
        public string Folder { get; set; } = "";
        public string Snippet { get; set; } = "";
        public long Mtime { get; set; }
        public float[] Vec { get; set; } = Array.Empty<float>();
    }

    public sealed class Store
    {
        public string Model { get; set; } = "";   // embeddings are model-specific (different dims)
        public List<Entry> Entries { get; set; } = new();
    }

    private static readonly string Path_ = Paths.File("search_index.json");
    private static readonly object _gate = new();
    private static Store? _cache;

    private static Store Load()
    {
        lock (_gate)
        {
            if (_cache is not null) return _cache;
            try { _cache = File.Exists(Path_) ? JsonSerializer.Deserialize<Store>(File.ReadAllText(Path_)) ?? new() : new(); }
            catch { _cache = new(); }
            return _cache;
        }
    }

    private static void Save()
    {
        lock (_gate)
        {
            try
            {
                var tmp = Path_ + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(_cache));
                if (File.Exists(Path_)) File.Replace(tmp, Path_, null); else File.Move(tmp, Path_);
            }
            catch { }
        }
    }

    public static int Count => Load().Entries.Count;

    /// <summary>Add/replace one document. Resets the index if the embedding model changed
    /// (vectors from different models aren't comparable).</summary>
    public static void Add(string path, string folder, string snippet, float[] vec, string model)
    {
        lock (_gate)
        {
            var s = Load();
            if (s.Model != model) { s.Entries.Clear(); s.Model = model; }
            s.Entries.RemoveAll(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
            long mtime = 0; try { mtime = new FileInfo(path).LastWriteTimeUtc.Ticks; } catch { }
            s.Entries.Add(new Entry { Path = path, Folder = folder, Snippet = Snip(snippet), Vec = vec, Mtime = mtime });
            Save();
        }
    }

    public static List<(Entry Entry, float Score)> Query(float[] qvec, int k)
    {
        var s = Load();
        return s.Entries
            .Where(e => e.Vec.Length == qvec.Length && File.Exists(e.Path))
            .Select(e => (Entry: e, Score: Embedder.Cosine(qvec, e.Vec)))
            .OrderByDescending(x => x.Score)
            .Take(k)
            .ToList();
    }

    /// <summary>Rebuild the whole index from the sorted destination folder.</summary>
    public static int Rebuild(string destRoot, string ollamaUrl, string model, Action<int, int>? progress = null)
    {
        if (!Directory.Exists(destRoot)) return 0;
        var files = Directory.EnumerateFiles(destRoot, "*", SearchOption.AllDirectories).Where(Indexable).ToList();
        var fresh = new Store { Model = model };
        int done = 0;
        foreach (var f in files)
        {
            progress?.Invoke(done, files.Count);
            string text; try { text = Extractor.Snippet(f, 3000); } catch { text = ""; }
            if (!string.IsNullOrWhiteSpace(text))
            {
                var vec = Embedder.Embed(text, ollamaUrl, model);
                if (vec is not null)
                {
                    long mtime = 0; try { mtime = new FileInfo(f).LastWriteTimeUtc.Ticks; } catch { }
                    fresh.Entries.Add(new Entry
                    {
                        Path = f,
                        Folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(f) ?? ""),
                        Snippet = Snip(text), Vec = vec, Mtime = mtime,
                    });
                }
            }
            done++;
        }
        progress?.Invoke(files.Count, files.Count);
        lock (_gate) { _cache = fresh; Save(); }
        return fresh.Entries.Count;
    }

    private static bool Indexable(string f)
    {
        var ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
        return ext is ".pdf" or ".txt" or ".md" or ".rtf" or ".csv" or ".log" or ".docx"
            or ".jpg" or ".jpeg" or ".png" or ".tif" or ".tiff" or ".bmp" or ".webp" or ".heic";
    }

    private static string Snip(string t) => t.Length > 500 ? t[..500] : t;
}
