using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIFileButler;

/// <summary>
/// Learns from user corrections. When the Butler sorts a file into category A
/// and the user moves it into category B (inside the sorted folder), it records
/// the file's distinctive tokens -> B and routes future matching files there
/// automatically — even in rules-only mode.
/// </summary>
public sealed class Learner
{
    private static readonly string LearnedPath =
        Path.Combine(AppContext.BaseDirectory, "learned_rules.json");
    private static readonly string PlacementsPath =
        Path.Combine(AppContext.BaseDirectory, "placements.json");

    private static readonly Dictionary<string, string> FolderToKey =
        Config.Categories.ToDictionary(c => c.Folder.ToLowerInvariant(), c => c.Key);

    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "img", "dsc", "scan", "screenshot", "file", "copy", "final", "new",
        "untitled", "document", "draft", "version", "temp", "download", "downloads",
        "the", "and", "for", "with", "from",
    };

    private sealed class Rule { public string Category { get; set; } = ""; public int Count { get; set; } }
    private sealed class Placement { public string Path { get; set; } = ""; public List<string> Tokens { get; set; } = new(); public string Category { get; set; } = ""; }

    private Dictionary<string, Rule> _rules;
    private List<Placement> _placements;

    public Learner()
    {
        _rules = LoadJson<Dictionary<string, Rule>>(LearnedPath) ?? new();
        _placements = LoadJson<List<Placement>>(PlacementsPath) ?? new();
    }

    public static List<string> Tokenize(string name)
    {
        var stem = Path.GetFileNameWithoutExtension(name).ToLowerInvariant();
        var parts = Regex.Split(stem, @"[^a-z0-9]+");
        var seen = new HashSet<string>();
        var outp = new List<string>();
        foreach (var t in parts)
        {
            if (t.Length < 3 || t.All(char.IsDigit) || Stop.Contains(t) || !seen.Add(t)) continue;
            outp.Add(t);
            if (outp.Count == 8) break;
        }
        return outp;
    }

    /// <summary>Consulted by the watcher before AI/rules.</summary>
    public string? Suggest(string name)
    {
        string? best = null;
        int bestCount = 0;
        foreach (var t in Tokenize(name))
            if (_rules.TryGetValue(t, out var r) && r.Count > bestCount)
            { best = r.Category; bestCount = r.Count; }
        return best;
    }

    public void RecordPlacement(string dst, string originalName, string category)
    {
        _placements.Add(new Placement { Path = dst, Tokens = Tokenize(originalName), Category = category });
        SaveJson(PlacementsPath, _placements);
    }

    /// <summary>Returns (token, newCategory) pairs learned from user moves this scan.</summary>
    public List<(string Token, string Category)> DetectCorrections(string destRoot)
    {
        var learned = new List<(string, string)>();
        var keep = new List<Placement>();

        foreach (var rec in _placements)
        {
            if (File.Exists(rec.Path)) { keep.Add(rec); continue; }

            var moved = FindUnder(destRoot, Path.GetFileName(rec.Path));
            if (moved is null) continue; // left the sorted area; stop tracking

            var newFolder = new DirectoryInfo(Path.GetDirectoryName(moved)!).Name.ToLowerInvariant();
            if (FolderToKey.TryGetValue(newFolder, out var newKey) && newKey != rec.Category)
            {
                foreach (var t in rec.Tokens)
                {
                    if (!_rules.TryGetValue(t, out var r)) r = new Rule { Category = newKey };
                    r.Category = newKey;
                    r.Count++;
                    _rules[t] = r;
                    learned.Add((t, newKey));
                }
                rec.Category = newKey;
            }
            rec.Path = moved;
            keep.Add(rec);
        }

        _placements = keep;
        if (learned.Count > 0) SaveJson(LearnedPath, _rules);
        SaveJson(PlacementsPath, _placements);
        return learned;
    }

    private static string? FindUnder(string root, string name)
    {
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateFiles(root, name, SearchOption.AllDirectories).FirstOrDefault();
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static T? LoadJson<T>(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : default; }
        catch { return default; }
    }

    private static void SaveJson<T>(string path, T data) =>
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOpts));
}
