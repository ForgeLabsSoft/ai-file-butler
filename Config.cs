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

    // User-defined rules: if a file's name or content contains Match, send it to
    // Folder (a path under the destination, e.g. "Invoices/Orange" or "University").
    // These win over the AI/rules classifier.
    public List<RuleEntry> Rules { get; set; } = new();
    // If the classifier's confidence (%) is below this, set the file aside in
    // "_Review" instead of guessing. 0 = always sort, never review.
    public int ReviewThreshold { get; set; } = 55;

    public string[] IgnoreExts { get; set; } = { ".part", ".crdownload", ".tmp", ".lnk" };

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string ConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "butler_config.json");

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

    /// <summary>First user rule whose Match is found in the text, or null.</summary>
    public RuleEntry? MatchRule(string nameAndContent)
    {
        var hay = nameAndContent.ToLowerInvariant();
        foreach (var r in Rules)
            if (!string.IsNullOrWhiteSpace(r.Match) && !string.IsNullOrWhiteSpace(r.Folder)
                && hay.Contains(r.Match.ToLowerInvariant()))
                return r;
        return null;
    }
}

public sealed class RuleEntry
{
    public string Match { get; set; } = "";   // keyword in filename or content
    public string Folder { get; set; } = "";  // destination sub-path

    public override string ToString() => $"\"{Match}\"  →  {Folder}";
}

// Source-generated JSON context — needed for trimming/single-file safety.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
                             ReadCommentHandling = JsonCommentHandling.Skip,
                             AllowTrailingCommas = true)]
[JsonSerializable(typeof(Config))]
internal partial class ConfigJson : JsonSerializerContext { }
