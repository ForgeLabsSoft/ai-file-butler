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
    }

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
    public static void Record(string file, string kind, string dateStr)
    {
        if (!TryDate(dateStr, out var date)) return;
        if (date < DateTime.Today.AddYears(-2)) return; // clearly not an active expiry
        var iso = date.ToString("yyyy-MM-dd");
        var list = Load();
        var existing = list.FirstOrDefault(x => string.Equals(x.File, file, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) { existing.Kind = kind; existing.Date = iso; }
        else list.Add(new Item { File = file, Kind = string.IsNullOrWhiteSpace(kind) ? "Document" : kind, Date = iso });
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
