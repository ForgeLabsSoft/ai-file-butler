using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIFileButler;

public readonly record struct Suggestion(
    string Category, string Filename, double Confidence, string Reason, string Backend)
{
    // Optional metadata used to build sub-folders (year/genre/artist/actor, and
    // for invoices whether the other party is a "client" or a "distributor").
    public string? Year { get; init; }
    public string? Genre { get; init; }
    public string? Person { get; init; }
    public string? Party { get; init; }
    // Set by a user rule: an explicit destination sub-path that overrides the
    // category/scheme folder entirely.
    public string? FolderOverride { get; init; }
    // For official documents: when it expires (yyyy-MM-dd) and what kind it is
    // (Passport, ID card, Visa, Insurance, Car tax…), for expiry reminders.
    public string? Expiry { get; init; }
    public string? DocKind { get; init; }
}

public interface IClassifier
{
    string Backend { get; }
    Suggestion Classify(FileInfo file, string snippet);
}

public static class Slug
{
    public static string Make(string name, string ext)
    {
        name = Regex.Replace(name, @"[^\w\-. ]", "").Trim().Replace(' ', '_');
        name = Regex.Replace(name, "_+", "_").Trim('_', '.');
        if (string.IsNullOrEmpty(name)) name = "file";
        if (!string.IsNullOrEmpty(ext) && !name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            name += ext;
        return name.Length > 120 ? name[..120] : name;
    }
}

/// <summary>Talks to a local Ollama model. Private + content-aware.</summary>
public sealed class OllamaClassifier : IClassifier
{
    public string Backend => "ollama";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private readonly string _url;
    private readonly string _model;

    public OllamaClassifier(string url, string model)
    {
        _url = url.TrimEnd('/');
        _model = model;
    }

    public static bool IsAvailable(string url)
    {
        try
        {
            // /api/tags can be slow (~2s) on a cold server; give it room so we
            // don't wrongly fall back to rules mode when Ollama is actually up.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var resp = Http.GetAsync($"{url.TrimEnd('/')}/api/tags", cts.Token).GetAwaiter().GetResult();
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private const string PromptTemplate =
        "You are a file-organizing assistant. Given a file's name and a snippet of its " +
        "content, choose the single best category and propose a clean, descriptive filename.\n\n" +
        "Allowed categories (use the key exactly): {0}\n\n" +
        "Filename rules: keep the original extension; concise, underscores, no spaces; " +
        "include vendor/subject and date if detectable, e.g. Invoice_BMW_2026-06.pdf. " +
        "For the FILENAME, don't invent a vendor/date that isn't in the name or content.\n\n" +
        "Also fill these metadata fields, else use \"\":\n" +
        "- year: 4-digit release/issue year if known.\n" +
        "- genre: for a movie or song you RECOGNIZE from the title, its main genre " +
        "(e.g. Action, Sci-Fi, Comedy, Rock, Pop). Fill this from your OWN knowledge of the title — " +
        "this is expected and is NOT inventing. E.g. Inception → Sci-Fi, The Matrix → Sci-Fi.\n" +
        "- person: for a recognized movie, the lead actor; for a song, the artist — from your own knowledge. " +
        "E.g. Inception → Leonardo DiCaprio.\n" +
        "- party: for an invoice/receipt, \"distributor\" if it is FROM a supplier/vendor/store, " +
        "\"client\" if it is a document issued TO a customer/client; otherwise \"\".\n" +
        "- expiry: if this is an official document with an expiry/valid-until/renewal date " +
        "(passport, ID card, driving licence, visa, residence permit, right to work, insurance, " +
        "car tax/MOT, warranty, subscription), the expiry date as yyyy-MM-dd; otherwise \"\". " +
        "Read it from the content; do not guess.\n" +
        "- doc_kind: a short label for that document type (e.g. Passport, ID card, Visa, " +
        "Car insurance, Home insurance, Pet insurance, Car tax, Driving licence), else \"\".\n\n" +
        "Respond with ONLY a JSON object: " +
        "{{\"category\":\"<key>\",\"filename\":\"<name.ext>\",\"confidence\":<0..1>,\"reason\":\"<short>\"," +
        "\"year\":\"\",\"genre\":\"\",\"person\":\"\",\"party\":\"\",\"expiry\":\"\",\"doc_kind\":\"\"}}\n\n" +
        "FILE NAME: {1}\nCONTENT SNIPPET:\n{2}\n";

    public Suggestion Classify(FileInfo file, string snippet)
    {
        var cats = string.Join(", ", Config.Categories.Select(c => c.Key));
        var prompt = string.Format(PromptTemplate, cats, file.Name,
            string.IsNullOrEmpty(snippet) ? "(no readable text; use the filename)" : snippet);

        var body = JsonSerializer.Serialize(new
        {
            model = _model,
            prompt,
            stream = false,
            format = "json",
            options = new { temperature = 0.1 },
        });

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var resp = Http.PostAsync($"{_url}/api/generate", content).GetAwaiter().GetResult();
        resp.EnsureSuccessStatusCode();
        var raw = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        using var outer = JsonDocument.Parse(raw);
        var inner = outer.RootElement.GetProperty("response").GetString() ?? "{}";
        using var doc = JsonDocument.Parse(inner);
        var root = doc.RootElement;

        var cat = GetStr(root, "category", "misc");
        if (!Config.Categories.Any(c => c.Key == cat)) cat = "misc";

        var rawName = GetStr(root, "filename", Path.GetFileNameWithoutExtension(file.Name));
        var fname = Slug.Make(Path.GetFileNameWithoutExtension(rawName), file.Extension);

        double conf = root.TryGetProperty("confidence", out var cEl)
                      && cEl.ValueKind is JsonValueKind.Number ? cEl.GetDouble() : 0.5;
        conf = Math.Clamp(conf, 0, 1);

        var reason = GetStr(root, "reason", "");
        if (reason.Length > 200) reason = reason[..200];

        return new Suggestion(cat, fname, conf, reason, Backend)
        {
            Year = Clean(GetStr(root, "year", "")),
            Genre = Clean(GetStr(root, "genre", "")),
            Person = Clean(GetStr(root, "person", "")),
            Party = GetStr(root, "party", "").Trim().ToLowerInvariant(),
            Expiry = GetStr(root, "expiry", "").Trim(),
            DocKind = GetStr(root, "doc_kind", "").Trim(),
        };
    }

    // Folder-safe, Title Case, capped — for genre/artist/actor sub-folders.
    private static string Clean(string s)
    {
        s = Regex.Replace(s ?? "", @"[^\w\- ]", "").Trim();
        return s.Length > 40 ? s[..40] : s;
    }

    private static string GetStr(JsonElement e, string prop, string fallback) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? fallback : fallback;
}

/// <summary>Heuristics on extension + filename. Always available, zero deps.</summary>
public sealed class RuleClassifier : IClassifier
{
    public string Backend => "rules";

    private static readonly Dictionary<string, string> ExtCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images / photos
        [".jpg"]="image",[".jpeg"]="image",[".jfif"]="image",[".png"]="image",[".gif"]="image",
        [".webp"]="image",[".bmp"]="image",[".heic"]="image",[".heif"]="image",[".svg"]="image",
        [".tif"]="image",[".tiff"]="image",[".ico"]="image",[".avif"]="image",[".raw"]="image",
        [".cr2"]="image",[".cr3"]="image",[".nef"]="image",[".arw"]="image",[".dng"]="image",[".orf"]="image",[".rw2"]="image",
        // Video
        [".mp4"]="movie",[".mkv"]="movie",[".mov"]="movie",[".avi"]="movie",[".wmv"]="movie",[".webm"]="movie",
        [".m4v"]="movie",[".mpg"]="movie",[".mpeg"]="movie",[".3gp"]="movie",[".flv"]="movie",[".ts"]="movie",
        [".m2ts"]="movie",[".mts"]="movie",[".vob"]="movie",[".ogv"]="movie",[".rm"]="movie",[".rmvb"]="movie",[".divx"]="movie",
        [".srt"]="movie",[".sub"]="movie",[".ass"]="movie",[".vtt"]="movie",[".ssa"]="movie",
        // Audio / music
        [".mp3"]="music",[".wav"]="music",[".flac"]="music",[".m4a"]="music",[".aac"]="music",[".ogg"]="music",
        [".wma"]="music",[".opus"]="music",[".aiff"]="music",[".aif"]="music",[".alac"]="music",[".mid"]="music",
        [".midi"]="music",[".amr"]="music",[".ape"]="music",[".dsf"]="music",
        // Installers / packages
        [".exe"]="installer",[".msi"]="installer",[".msix"]="installer",[".appx"]="installer",[".appxbundle"]="installer",
        [".apk"]="installer",[".aab"]="installer",[".deb"]="installer",[".rpm"]="installer",[".dmg"]="installer",
        [".pkg"]="installer",[".bat"]="installer",[".cmd"]="installer",[".jar"]="installer",
        // Archives / disk images
        [".zip"]="archive",[".rar"]="archive",[".7z"]="archive",[".tar"]="archive",[".gz"]="archive",
        [".bz2"]="archive",[".xz"]="archive",[".tgz"]="archive",[".zst"]="archive",[".lz"]="archive",[".lzma"]="archive",
        [".cab"]="archive",[".arj"]="archive",[".iso"]="archive",[".img"]="archive",[".vhd"]="archive",[".vhdx"]="archive",
        // Code / dev
        [".py"]="code",[".js"]="code",[".mjs"]="code",[".ts"]="code",[".tsx"]="code",[".jsx"]="code",[".cs"]="code",
        [".java"]="code",[".kt"]="code",[".c"]="code",[".h"]="code",[".cpp"]="code",[".cc"]="code",[".hpp"]="code",
        [".go"]="code",[".rs"]="code",[".rb"]="code",[".php"]="code",[".swift"]="code",[".scala"]="code",[".lua"]="code",
        [".pl"]="code",[".r"]="code",[".dart"]="code",[".vue"]="code",[".sql"]="code",[".sh"]="code",[".ps1"]="code",
        [".html"]="code",[".htm"]="code",[".css"]="code",[".scss"]="code",[".json"]="code",[".xml"]="code",
        [".yaml"]="code",[".yml"]="code",[".toml"]="code",[".ini"]="code",[".gradle"]="code",[".ipynb"]="code",
        // Documents / office / data
        [".doc"]="document",[".docx"]="document",[".txt"]="document",[".md"]="document",[".rtf"]="document",
        [".odt"]="document",[".pptx"]="document",[".ppt"]="document",[".odp"]="document",[".xlsx"]="document",
        [".xls"]="document",[".ods"]="document",[".csv"]="document",[".tsv"]="document",[".pdf"]="document",
        [".pages"]="document",[".numbers"]="document",[".key"]="document",[".tex"]="document",[".log"]="document",
        // Ebooks
        [".epub"]="ebook",[".mobi"]="ebook",[".azw"]="ebook",[".azw3"]="ebook",[".fb2"]="ebook",[".djvu"]="ebook",[".cbz"]="ebook",[".cbr"]="ebook",
        // Fonts
        [".ttf"]="font",[".otf"]="font",[".woff"]="font",[".woff2"]="font",[".eot"]="font",[".fon"]="font",
        // Design / 3D / CAD
        [".psd"]="design",[".ai"]="design",[".xd"]="design",[".fig"]="design",[".sketch"]="design",[".indd"]="design",
        [".eps"]="design",[".cdr"]="design",[".afphoto"]="design",[".afdesign"]="design",
        [".stl"]="design",[".obj"]="design",[".fbx"]="design",[".blend"]="design",[".dwg"]="design",[".dxf"]="design",[".step"]="design",[".3mf"]="design",
    };

    private static readonly Dictionary<string, string[]> Keywords = new()
    {
        ["invoice"] = new[] { "invoice", "factura", "rechnung", "facture" },
        ["receipt"] = new[] { "receipt", "chitanta", "bon", "checklist" },
    };

    public Suggestion Classify(FileInfo file, string snippet)
    {
        var hay = (file.Name + "\n" + snippet).ToLowerInvariant();
        string? cat = null;
        bool byKeyword = false;
        foreach (var (key, words) in Keywords)
            if (words.Any(w => hay.Contains(w))) { cat = key; byKeyword = true; break; }

        cat ??= ExtCategory.GetValueOrDefault(file.Extension, "misc");

        var fname = Slug.Make(Path.GetFileNameWithoutExtension(file.Name), file.Extension);
        double conf = cat != "misc" ? 0.8 : 0.3;
        return new Suggestion(cat, fname, conf, byKeyword ? "matched by keyword" : "matched by extension", Backend);
    }
}

public static class ClassifierFactory
{
    public static IClassifier Get(Config cfg) =>
        OllamaClassifier.IsAvailable(cfg.OllamaUrl)
            ? new OllamaClassifier(cfg.OllamaUrl, cfg.OllamaModel)
            : new RuleClassifier();
}
