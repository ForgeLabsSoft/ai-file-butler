using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIFileButler;

/// <summary>
/// App configuration. Mirrors the Python prototype's butler_config.json so the
/// two stay interchangeable. Drop a butler_config.json next to the .exe to
/// override any of these.
/// </summary>
public sealed class Config
{
    // category key -> destination subfolder name (insertion order = display order)
    public static readonly (string Key, string Folder)[] Categories =
    {
        ("invoice",   "Invoices"),
        ("receipt",   "Receipts"),
        ("document",  "Documents"),
        ("image",     "Images"),
        ("installer", "Installers"),
        ("archive",   "Archives"),
        ("code",      "Code"),
        ("music",     "Music"),
        ("movie",     "Movies"),
        ("ebook",     "Books"),
        ("font",      "Fonts"),
        ("design",    "Design"),
        ("misc",      "Misc"),
    };

    public static string FolderFor(string key)
    {
        foreach (var (k, folder) in Categories)
            if (k == key) return folder;
        return "Misc";
    }

    public List<string> WatchDirs { get; set; } = new()
    {
        Path.Combine(Home, "Downloads"),
    };

    public string DestRoot { get; set; } = Path.Combine(Home, "Downloads", "_Sorted");
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama3.1:8b";
    // Dedicated embedding model for semantic search (small; pull once with
    // `ollama pull nomic-embed-text`). Separate from the chat model above.
    public string EmbedModel { get; set; } = "nomic-embed-text";
    public string Language { get; set; } = "en";
    public int MaxContentChars { get; set; } = 1500;
    public int MinAgeSeconds { get; set; } = 10;
    public int PollIntervalSeconds { get; set; } = 8;

    // How to sub-sort within a category. Values: "none","alpha","year","genre","artist","actor".
    public string MusicBy { get; set; } = "artist";   // Music/<Artist>/…
    public string MovieBy { get; set; } = "genre";    // Movies/<Genre>/…
    public string ImageBy { get; set; } = "date";     // date|location|person|alpha|none
    public bool SeparateInvoiceParties { get; set; } = true; // Invoices/Clients vs Distributors

    public bool AutoOrganize { get; set; } = true;   // persisted auto-move state
    public bool FirstRunDone { get; set; } = false;  // show the welcome screen once
    public bool DarkMode { get; set; } = false;      // dark theme for the windows
    public double FaceThreshold { get; set; } = 0.35; // face-match similarity (0..1)
    // Auto-read expiry dates from documents (passport, visa, insurance…) and
    // remind you before they lapse. Works even without the AI (regex over text).
    public bool ExpiryScan { get; set; } = true;
    // How many days before an expiry to remind you. Up to three lead times — the
    // default is 3 months, 1 month and 1 week. A document can override these.
    public List<int> ReminderDays { get; set; } = new() { 90, 30, 7 };

    // User-defined rules: if a file's name or content contains Match, send it to
    // Folder (a path under the destination, e.g. "Invoices/Orange" or "University").
    // These win over the AI/rules classifier.
    public List<RuleEntry> Rules { get; set; } = new();
    // If the classifier's confidence (%) is below this, set the file aside in
    // "_Review" instead of guessing. 0 = always sort, never review.
    public int ReviewThreshold { get; set; } = 55;

    public string[] IgnoreExts { get; set; } = { ".part", ".crdownload", ".tmp", ".lnk" };

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string ConfigPath => Paths.File("butler_config.json");

    public static Config Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize(json, ConfigJson.Default.Config);
                if (loaded is not null) return loaded.Normalized();
            }
        }
        catch (Exception ex)
        {
            // Bad config shouldn't be fatal — fall back to defaults.
            Console.Error.WriteLine($"[config] ignoring bad file: {ex.Message}");
        }
        return new Config().Normalized();
    }

    private Config Normalized()
    {
        WatchDirs = WatchDirs.Select(Environment.ExpandEnvironmentVariables).ToList();
        DestRoot = Environment.ExpandEnvironmentVariables(DestRoot);
        return this;
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, ConfigJson.Default.Config);
        File.WriteAllText(ConfigPath, json);
    }

    /// <summary>The first enabled rule that matches a file, or null. Rules are tried
    /// top to bottom (first match wins). Only name/content/extension are available
    /// here — those are known before the file is classified.</summary>
    public RuleEntry? EvaluateRules(string name, string content, string ext)
    {
        foreach (var r in Rules)
        {
            if (!r.Enabled || string.IsNullOrWhiteSpace(r.Match)) continue;
            var hay = r.Field switch
            {
                "name" => name,
                "content" => content,
                "ext" => ext,
                _ => name + "\n" + content,
            };
            if (RuleEntry.Matches(hay, r.Op, r.Match)) return r;
        }
        return null;
    }
}

public sealed class RuleEntry
{
    public string Match { get; set; } = "";     // the value to test (name kept for back-compat)
    public string Folder { get; set; } = "";    // destination sub-path for the "folder" action
    public bool Enabled { get; set; } = true;
    public string Field { get; set; } = "any";  // any (name+content) | name | content | ext
    public string Op { get; set; } = "contains"; // contains | equals | starts | ends | regex
    public string Action { get; set; } = "folder"; // folder | review | skip

    public static readonly string[] Fields = { "any", "name", "content", "ext" };
    public static readonly string[] Ops = { "contains", "equals", "starts", "ends", "regex" };
    public static readonly string[] Actions = { "folder", "review", "skip" };

    public static bool Matches(string hay, string op, string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        var h = hay.ToLowerInvariant();
        var v = value.ToLowerInvariant().Trim();
        return op switch
        {
            "equals" => h.Trim() == v,
            "starts" => h.TrimStart().StartsWith(v, StringComparison.Ordinal),
            "ends" => h.TrimEnd().EndsWith(v, StringComparison.Ordinal),
            "regex" => TryRegex(hay, value),
            _ => TextMatch.ContainsWord(h, v),
        };
    }

    private static bool TryRegex(string hay, string pattern)
    {
        try { return System.Text.RegularExpressions.Regex.IsMatch(hay, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase); }
        catch { return false; }
    }

    public override string ToString()
    {
        var act = Action == "folder" ? "→ " + Folder : Action == "review" ? "→ Review" : "→ Skip";
        var f = Field == "any" ? "name/content" : Field == "ext" ? "extension" : Field;
        var flag = Enabled ? "" : "(off) ";
        return $"{flag}{f} {Op} \"{Match}\"  {act}";
    }
}

// Source-generated JSON context — needed for trimming/single-file safety.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
                             ReadCommentHandling = JsonCommentHandling.Skip,
                             AllowTrailingCommas = true)]
[JsonSerializable(typeof(Config))]
internal partial class ConfigJson : JsonSerializerContext { }
