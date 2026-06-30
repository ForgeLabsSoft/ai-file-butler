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

    private static readonly string Path_ =
        System.IO.Path.Combine(AppContext.BaseDirectory, "reminders.json");
    private static List<Item>? _cache;

    private static List<Item> Load()
    {
        if (_cache is not null) return _cache;
        try { _cache = File.Exists(Path_) ? JsonSerializer.Deserialize<List<Item>>(File.ReadAllText(Path_)) ?? new() : new(); }
        catch { _cache = new(); }
        return _cache;
    }

    private static void Save() => File.WriteAllText(Path_, JsonSerializer.Serialize(_cache));

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
            Name = name, Country = country, Id = id,
        });
        Save();
    }

    /// <summary>Apply user edits (ID, name, country, kind, date, lead times) to a stored item.</summary>
    public static void Update(string file, string id, string name, string country, string kind, string dateStr, string leadDaysCsv)
    {
        var item = Load().FirstOrDefault(x => string.Equals(x.File, file, StringComparison.OrdinalIgnoreCase));
        if (item is null) return;
        item.Id = id.Trim();
        item.Name = name.Trim();
        item.Country = country.Trim();
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
