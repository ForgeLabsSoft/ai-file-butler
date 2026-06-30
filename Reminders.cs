using System.Globalization;
using System.Text.Json;

namespace AIFileButler;

/// <summary>Tracks official documents that expire (passport, ID, visa, right to
/// work, insurances, car tax…) so the Butler can remind you before they lapse.
/// The expiry date is read from the document content by the AI. Stored locally.</summary>
public static class Reminders
{
    public sealed class Item
    {
        public string File { get; set; } = "";   // where the document was filed
        public string Kind { get; set; } = "";    // Passport, Visa, Car insurance…
        public string Date { get; set; } = "";    // yyyy-MM-dd
        public string Id { get; set; } = "";       // optional, user-filled (doc/policy number)
        public string Name { get; set; } = "";     // holder name (scanned, editable)
        public string Country { get; set; } = "";  // issuing country / origin (scanned, editable)
        public List<int> LeadDays { get; set; } = new();  // per-doc lead times; empty = use global
        public List<int> Notified { get; set; } = new();  // lead thresholds already notified
        public string Category { get; set; } = "";  // "id" | "renewal" | "task"
        public string Title { get; set; } = "";     // for manual tasks (e.g. "Take dog to vet")
        public string Repeat { get; set; } = "";     // "", daily, weekly, monthly, yearly
        public string Start { get; set; } = "";      // issue / start date (yyyy-MM-dd), optional

        /// <summary>What to show in the main column: a task's title, a holder name,
        /// otherwise the file name.</summary>
        public string Label =>
            Title.Length > 0 ? Title :
            Name.Length > 0 ? Name :
            File.StartsWith("task://") ? "Task" : System.IO.Path.GetFileName(File);
    }

    /// <summary>Three buckets: identity documents, renewals (insurance, tax, subs…),
    /// and personal tasks. Auto-classified from the document kind.</summary>
    public static string Categorize(string kind)
    {
        var k = (kind ?? "").ToLowerInvariant();
        string[] id = { "passport", "visa", "id card", "identity", "driving licence", "driving license",
                        "residence", "right to work", "national insurance", "citizen" };
        return id.Any(k.Contains) ? "id" : "renewal";
    }

    /// <summary>Add a manual personal task (no document), optionally recurring.</summary>
    public static void AddTask(string title, string dateStr, string repeat, List<int>? leadDays = null)
    {
        if (string.IsNullOrWhiteSpace(title) || !TryDate(dateStr, out var d)) return;
        Load().Add(new Item
        {
            File = "task://" + Guid.NewGuid().ToString("N"),
            Category = "task", Title = title.Trim(), Kind = "Task",
            Date = d.ToString("yyyy-MM-dd"), Repeat = Normalize(repeat),
            LeadDays = leadDays ?? new(),
        });
        Save();
    }

    /// <summary>Add a document by hand (no file): ID type, number, holder, country,
    /// start date and expiry. Useful for tracking people / IDs you don't have a scan of.</summary>
    public static void AddDocument(string idType, string id, string name, string country,
                                   string startIso, string expiresIso, List<int>? leadDays = null)
    {
        if (!TryDate(expiresIso, out var d)) return; // an expiry is what we remind on
        Load().Add(new Item
        {
            File = "doc://" + Guid.NewGuid().ToString("N"),
            Category = Categorize(idType),
            Kind = string.IsNullOrWhiteSpace(idType) ? "Document" : idType.Trim(),
            Id = id.Trim(), Name = name.Trim(), Country = country.Trim(),
            Start = TryDate(startIso, out var s) ? s.ToString("yyyy-MM-dd") : "",
            Date = d.ToString("yyyy-MM-dd"), LeadDays = leadDays ?? new(),
        });
        Save();
    }

    /// <summary>Roll any past-due recurring item forward to its next occurrence so
    /// it reminds again next cycle (weekly milk run, yearly vet visit, etc.).</summary>
    public static void RollRecurring()
    {
        bool changed = false;
        foreach (var i in Load())
        {
            var rep = Normalize(i.Repeat);
            if (rep.Length == 0 || !TryDate(i.Date, out var d) || d.Date >= DateTime.Today) continue;
            var next = d; int guard = 0;
            while (next.Date < DateTime.Today && guard++ < 4000) next = Advance(next, rep);
            if (next.Date != d.Date) { i.Date = next.ToString("yyyy-MM-dd"); i.Notified.Clear(); changed = true; }
        }
        if (changed) Save();
    }

    private static DateTime Advance(DateTime d, string repeat) => repeat switch
    {
        "daily" => d.AddDays(1),
        "weekly" => d.AddDays(7),
        "monthly" => d.AddMonths(1),
        "yearly" => d.AddYears(1),
        _ => d.AddYears(1000),
    };

    private static string Normalize(string? repeat)
    {
        var r = (repeat ?? "").Trim().ToLowerInvariant();
        return r is "daily" or "weekly" or "monthly" or "yearly" ? r : "";
    }

    // for the repeat dropdowns: parallel value/label-key arrays
    public static readonly string[] RepeatVals = { "", "daily", "weekly", "monthly", "yearly" };
    public static readonly string[] RepeatKeys = { "repeat_none", "repeat_daily", "repeat_weekly", "repeat_monthly", "repeat_yearly" };

    /// <summary>Localized repeat label for the list ("" when one-off).</summary>
    public static string RepeatLabel(string repeat)
    {
        int i = Array.IndexOf(RepeatVals, Normalize(repeat));
        return i <= 0 ? "" : L.S(RepeatKeys[i]);
    }

    /// <summary>The lead times to use for an item: its own override, or the global
    /// default. Sorted soonest-expiry-first, positives only.</summary>
    public static List<int> LeadFor(Item i, List<int> global) =>
        (i.LeadDays.Count > 0 ? i.LeadDays : global)
        .Where(x => x > 0).Distinct().OrderByDescending(x => x).ToList();

    /// <summary>Mark lead thresholds as already notified for a document.</summary>
    public static void MarkNotified(string file, IEnumerable<int> thresholds)
    {
        var i = Load().FirstOrDefault(x => string.Equals(x.File, file, StringComparison.OrdinalIgnoreCase));
        if (i is null) return;
        foreach (var t in thresholds) if (!i.Notified.Contains(t)) i.Notified.Add(t);
        Save();
    }

    /// <summary>Parse a "90, 30, 7" string into a clean day list.</summary>
    public static List<int> ParseDays(string csv) => csv
        .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(s => int.TryParse(s.Trim(), out var n) ? n : 0)
        .Where(n => n > 0).Distinct().OrderByDescending(n => n).ToList();

    private static readonly string Path_ = Paths.File("reminders.json");
    private static readonly object _gate = new();
    private static List<Item>? _cache;

    private static List<Item> Load()
    {
        lock (_gate)
        {
            // Fall back to the .bak if the main file is missing/corrupt, so a crash
            // mid-write can never silently wipe every reminder the user had.
            return _cache ??= ReadFile(Path_) ?? ReadFile(Path_ + ".bak") ?? new();
        }
    }

    private static List<Item>? ReadFile(string p)
    {
        try { return File.Exists(p) ? JsonSerializer.Deserialize<List<Item>>(File.ReadAllText(p)) : null; }
        catch { return null; }
    }

    // Atomic: write a temp file then swap it in, keeping the previous good copy as .bak.
    private static void Save()
    {
        lock (_gate)
        {
            try
            {
                var json = JsonSerializer.Serialize(_cache);
                var tmp = Path_ + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(Path_)) File.Replace(tmp, Path_, Path_ + ".bak");
                else File.Move(tmp, Path_);
            }
            catch { /* best effort; the in-memory cache is still intact */ }
        }
    }

    public static bool TryDate(string s, out DateTime d) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
        || DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out d);

    /// <summary>Record/refresh a document's expiry. Ignores junk or very old dates.</summary>
    public static void Record(string file, string kind, string dateStr, string name = "", string country = "", string id = "")
    {
        if (!TryDate(dateStr, out var date)) return;
        if (date < DateTime.Today.AddYears(-2)) return; // clearly not an active expiry
        var iso = date.ToString("yyyy-MM-dd");
        var list = Load();
        var existing = list.FirstOrDefault(x => string.Equals(x.File, file, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Kind = string.IsNullOrWhiteSpace(kind) ? existing.Kind : kind;
            if (existing.Date != iso) existing.Notified.Clear(); // date moved → remind again
            existing.Date = iso;
            // only fill scanned fields if we found something and the user hasn't set one
            if (existing.Name.Length == 0 && name.Length > 0) existing.Name = name;
            if (existing.Country.Length == 0 && country.Length > 0) existing.Country = country;
            if (existing.Id.Length == 0 && id.Length > 0) existing.Id = id;
        }
        else list.Add(new Item
        {
            File = file, Kind = string.IsNullOrWhiteSpace(kind) ? "Document" : kind, Date = iso,
            Name = name, Country = country, Id = id, Category = Categorize(kind),
        });
        Save();
    }

    /// <summary>Apply user edits to a stored item (documents or tasks).</summary>
    public static void Update(string file, string id, string name, string country, string kind,
                              string dateStr, string leadDaysCsv, string title = "", string repeat = "", string start = "")
    {
        var item = Load().FirstOrDefault(x => string.Equals(x.File, file, StringComparison.OrdinalIgnoreCase));
        if (item is null) return;
        item.Id = id.Trim();
        item.Name = name.Trim();
        item.Country = country.Trim();
        item.Title = title.Trim();
        item.Repeat = Normalize(repeat);
        item.Start = TryDate(start, out var sd) ? sd.ToString("yyyy-MM-dd") : "";
        if (!string.IsNullOrWhiteSpace(kind)) item.Kind = kind.Trim();
        if (TryDate(dateStr, out var d))
        {
            var iso = d.ToString("yyyy-MM-dd");
            if (item.Date != iso) item.Notified.Clear();
            item.Date = iso;
        }
        var leads = ParseDays(leadDaysCsv);
        if (!leads.SequenceEqual(item.LeadDays)) item.Notified.Clear(); // schedule changed → remind again
        item.LeadDays = leads;
        Save();
    }

    public static List<Item> All() =>
        Load().Where(i => TryDate(i.Date, out _)).OrderBy(i => i.Date).ToList();

    public static int DaysLeft(Item i) => TryDate(i.Date, out var d) ? (d.Date - DateTime.Today).Days : int.MaxValue;

    /// <summary>Documents expiring within <paramref name="days"/> (incl. already expired).</summary>
    public static List<Item> Upcoming(int days) =>
        All().Where(i => DaysLeft(i) <= days).ToList();

    public static void Remove(string file)
    {
        Load().RemoveAll(x => string.Equals(x.File, file, StringComparison.OrdinalIgnoreCase));
        Save();
    }
}
