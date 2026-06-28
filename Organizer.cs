using System.Text.Json;

namespace AIFileButler;

public sealed record Plan(string Src, string Dst, string Category, double Confidence,
                          string Reason, string Backend)
{
    public string DstName => Path.GetFileName(Dst);
}

/// <summary>Safe file moves with a reversible undo log (one JSON line per move).</summary>
public static class Organizer
{
    private static readonly string UndoLog =
        Path.Combine(AppContext.BaseDirectory, "undo_log.jsonl");

    public static Plan BuildPlan(FileInfo src, Suggestion s, Config cfg)
    {
        // A user rule routes straight to an explicit folder, bypassing categories.
        if (!string.IsNullOrWhiteSpace(s.FolderOverride))
        {
            var rf = Path.Combine(cfg.DestRoot, SafePath(s.FolderOverride));
            var rdst2 = Dedupe(Path.Combine(rf, s.Filename));
            return new Plan(src.FullName, rdst2, s.Category, s.Confidence, s.Reason, s.Backend);
        }

        // When unsure, don't guess — set the file aside for the user to review.
        if (cfg.ReviewThreshold > 0 && s.Confidence * 100 < cfg.ReviewThreshold)
        {
            var rdst = Dedupe(Path.Combine(cfg.DestRoot, "_Review", s.Filename));
            return new Plan(src.FullName, rdst, "review", s.Confidence,
                            $"low confidence ({s.Confidence:P0}) — set aside", s.Backend);
        }

        var folder = Path.Combine(cfg.DestRoot, Config.FolderFor(s.Category));
        var sub = SubFolder(s, cfg, src);
        if (!string.IsNullOrEmpty(sub)) folder = Path.Combine(folder, Slug.Make(sub, ""));
        var dst = Dedupe(Path.Combine(folder, s.Filename));
        return new Plan(src.FullName, dst, s.Category, s.Confidence, s.Reason, s.Backend);
    }

    /// <summary>The extra sub-folder a file goes into, based on the chosen scheme
    /// (music by artist/genre/year, movies by genre/actor/year, invoices by party).</summary>
    private static string SubFolder(Suggestion s, Config cfg, FileInfo src)
    {
        switch (s.Category)
        {
            case "music":
                return Pick(cfg.MusicBy, s, s.Person);
            case "movie":
                // films use AI genre/actor/year; phone videos use date/location
                return cfg.MovieBy switch
                {
                    "date" => PhotoMeta.DateFolder(src),
                    "location" => PhotoMeta.LocationFolder(src),
                    _ => Pick(cfg.MovieBy, s, s.Person),
                };
            case "image":
                return cfg.ImageBy switch
                {
                    "date" => PhotoMeta.DateFolder(src),
                    "location" => PhotoMeta.LocationFolder(src),
                    "person" => PhotoMeta.PeopleFolder(src),
                    "alpha" => FirstLetter(null, s.Filename),
                    _ => "",
                };
            case "invoice":
            case "receipt":
                if (cfg.SeparateInvoiceParties)
                {
                    if (s.Party == "client") return "Clients";
                    if (s.Party == "distributor") return "Distributors";
                }
                return "";
            default:
                return "";
        }
    }

    private static string Pick(string scheme, Suggestion s, string? personField) => scheme switch
    {
        "genre" => Safe(s.Genre),
        "year" => Safe(s.Year),
        "artist" or "actor" or "person" => Safe(personField),
        "alpha" => FirstLetter(personField, s.Filename),
        _ => "",
    };

    private static string Safe(string? v) => string.IsNullOrWhiteSpace(v) ? "" : v.Trim();

    private static string FirstLetter(string? person, string filename)
    {
        var basis = !string.IsNullOrWhiteSpace(person) ? person! : filename;
        var c = basis.TrimStart().FirstOrDefault(char.IsLetterOrDigit);
        if (c == default) return "#";
        c = char.ToUpperInvariant(c);
        return char.IsDigit(c) ? "0-9" : c.ToString();
    }

    // Turn a user-entered "Invoices/Orange" into a safe nested relative path.
    private static string SafePath(string raw)
    {
        var parts = raw.Split('/', '\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                       .Select(p => Slug.Make(p, ""))
                       .Where(p => p.Length > 0)
                       .Take(5);
        var path = Path.Combine(parts.ToArray());
        return string.IsNullOrEmpty(path) ? "_Rules" : path;
    }

    private static string Dedupe(string dst)
    {
        if (!File.Exists(dst)) return dst;
        var dir = Path.GetDirectoryName(dst)!;
        var stem = Path.GetFileNameWithoutExtension(dst);
        var ext = Path.GetExtension(dst);
        for (int i = 2; ; i++)
        {
            var cand = Path.Combine(dir, $"{stem}_{i}{ext}");
            if (!File.Exists(cand)) return cand;
        }
    }

    public static void Apply(Plan plan)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(plan.Dst)!);
        var final = Dedupe(plan.Dst); // re-check at apply time
        File.Move(plan.Src, final);
        AppendUndo(plan.Src, final);
    }

    private static void AppendUndo(string src, string dst)
    {
        var rec = JsonSerializer.Serialize(new
        {
            ts = DateTimeOffset.UtcNow.ToString("o"),
            from = src,
            to = dst,
        });
        File.AppendAllText(UndoLog, rec + Environment.NewLine);
    }

    public static int UndoLast(int n)
    {
        if (!File.Exists(UndoLog)) return 0;
        var lines = File.ReadAllLines(UndoLog).Where(l => l.Length > 0).ToList();
        int reverted = 0;
        for (int idx = lines.Count - 1; idx >= 0 && reverted < n; idx--)
        {
            using var doc = JsonDocument.Parse(lines[idx]);
            var to = doc.RootElement.GetProperty("to").GetString()!;
            var from = doc.RootElement.GetProperty("from").GetString()!;
            if (File.Exists(to))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(from)!);
                File.Move(to, Dedupe(from));
                reverted++;
            }
            lines.RemoveAt(idx);
        }
        File.WriteAllLines(UndoLog, lines);
        return reverted;
    }

    /// <summary>The move history, newest first (for the History window).</summary>
    public static List<HistoryItem> History(int max = 300)
    {
        var items = new List<HistoryItem>();
        if (!File.Exists(UndoLog)) return items;
        foreach (var line in File.ReadAllLines(UndoLog))
        {
            if (line.Length == 0) continue;
            try
            {
                using var d = JsonDocument.Parse(line);
                var r = d.RootElement;
                items.Add(new HistoryItem(
                    r.GetProperty("ts").GetString() ?? "",
                    r.GetProperty("from").GetString() ?? "",
                    r.GetProperty("to").GetString() ?? "", line));
            }
            catch { /* skip malformed line */ }
        }
        items.Reverse(); // newest first
        return max > 0 && items.Count > max ? items.GetRange(0, max) : items;
    }

    /// <summary>Reverse one specific move and drop it from the log.</summary>
    public static bool UndoOne(HistoryItem item)
    {
        if (!File.Exists(UndoLog)) return false;
        bool moved = false;
        if (File.Exists(item.To))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(item.From)!);
            File.Move(item.To, Dedupe(item.From));
            moved = true;
        }
        var lines = File.ReadAllLines(UndoLog).Where(l => l.Length > 0 && l != item.Raw).ToList();
        File.WriteAllLines(UndoLog, lines);
        return moved;
    }
}

public sealed record HistoryItem(string Ts, string From, string To, string Raw);
