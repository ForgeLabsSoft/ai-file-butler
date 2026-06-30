using System.Globalization;
using System.Text.RegularExpressions;

namespace AIFileButler;

/// <summary>
/// Finds an expiry date in a document's text (OCR/PDF/filename) WITHOUT needing
/// the AI — a regex pass over many real-world date formats, including passport
/// style "26 NOV /NOV 24". To avoid false alarms on random photos, it only
/// returns a reminder when the text looks like an official document (a known
/// document kind) or explicitly mentions an expiry. Best-effort; the user can
/// always delete a wrong reminder.
/// </summary>
public static class ExpiryScanner
{
    // word -> month number. Matched by 3-letter prefix where unambiguous, plus
    // a few longer EN / RO / FR spellings.
    private static readonly (string Prefix, int Month)[] Months =
    {
        ("jan", 1), ("ian", 1), ("feb", 2), ("febr", 2), ("fev", 2),
        ("mar", 3), ("mart", 3), ("apr", 4), ("avr", 4), ("may", 5), ("mai", 5),
        ("jun", 6), ("iun", 6), ("juin", 6), ("jul", 7), ("iul", 7), ("juil", 7),
        ("aug", 8), ("aou", 8), ("sep", 9), ("oct", 10),
        ("nov", 11), ("noi", 11), ("dec", 12),
    };

    private static readonly string[] ExpiryWords =
    {
        "expiry", "expiration", "expires", "expire", "valid until", "valid to",
        "date of expiry", "expiră", "expira", "valabil", "valabilitate", "valable",
    };

    // keyword(s) -> friendly document kind. Most specific first.
    private static readonly (string[] Keys, string Kind)[] Kinds =
    {
        (new[] { "passport", "pasaport", "paşaport" }, "Passport"),
        (new[] { "residence permit", "biometric residence", "permis de ședere", "permis de sedere" }, "Residence permit"),
        (new[] { "right to work", "share code" }, "Right to work"),
        (new[] { "visa", "viză", "viza" }, "Visa"),
        (new[] { "driving licence", "driver license", "driving license", "permis de conducere" }, "Driving licence"),
        (new[] { "national insurance" }, "National Insurance"),
        (new[] { "identity card", "id card", "carte de identitate", "buletin" }, "ID card"),
        (new[] { "mot certificate", " mot " }, "MOT"),
        (new[] { "road tax", "vehicle tax", "car tax", "taxă auto", "taxa auto", "rovinieta", "rovinietă" }, "Car tax"),
        (new[] { "home insurance", "house insurance", "buildings insurance", "contents insurance", "asigurare locuință", "asigurare locuinta", "asigurarea casei" }, "Home insurance"),
        (new[] { "pet insurance", "asigurare animal", "asigurarea animal" }, "Pet insurance"),
        (new[] { "car insurance", "motor insurance", "vehicle insurance", "auto insurance", "rca", "asigurare auto", "asigurarea mașinii", "asigurarea masinii" }, "Car insurance"),
        (new[] { "tenancy", "lease agreement", "contract de închiriere", "contract de inchiriere" }, "Tenancy"),
        (new[] { "warranty", "guarantee", "garanție", "garantie" }, "Warranty"),
        (new[] { "insurance", "asigurare", "asigurarea" }, "Insurance"),
        (new[] { "certificate", "certificat" }, "Certificate"),
    };

    public sealed record Result(string Kind, string Date);

    /// <summary>Scan filename + content for an expiry. Null if nothing convincing.</summary>
    public static Result? Scan(string fileName, string? text)
    {
        var hay = (fileName + "\n" + (text ?? "")).Replace(' ', ' ');
        var lower = hay.ToLowerInvariant();

        string kind = GuessKind(lower);
        bool hasExpiryWord = ExpiryWords.Any(w => lower.Contains(w));
        // Only act on things that look like real documents — avoids turning every
        // photo with a date in it into a reminder.
        if (kind.Length == 0 && !hasExpiryWord) return null;

        var date = FindExpiry(hay, lower);
        if (date is null) return null;
        return new Result(kind.Length == 0 ? "Document" : kind, date);
    }

    private static string GuessKind(string lower)
    {
        foreach (var (keys, kind) in Kinds)
            if (keys.Any(k => lower.Contains(k))) return kind;
        return "";
    }

    private sealed record Cand(DateTime Date, int Index);

    private static string? FindExpiry(string hay, string lower)
    {
        var cands = new List<Cand>();
        void Add(DateTime? d, int idx) { if (d is DateTime dt && Plausible(dt)) cands.Add(new Cand(dt, idx)); }

        // day month-name year, optionally bilingual "26 NOV /NOV 24"
        foreach (Match m in Regex.Matches(hay, @"\b(\d{1,2})\s+([A-Za-zĂÂÎăâî]{3,9})\.?(?:\s*/\s*[A-Za-zÉéûô]{3,9})?\s+(\d{2,4})\b"))
            Add(FromParts(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value), m.Index);

        // month-name day, year  (e.g. "Nov 26, 2024")
        foreach (Match m in Regex.Matches(hay, @"\b([A-Za-zĂÂÎăâî]{3,9})\.?\s+(\d{1,2}),?\s+(\d{2,4})\b"))
            Add(FromParts(m.Groups[2].Value, m.Groups[1].Value, m.Groups[3].Value), m.Index);

        // ISO  yyyy-mm-dd
        foreach (Match m in Regex.Matches(hay, @"\b(\d{4})[.\-/](\d{1,2})[.\-/](\d{1,2})\b"))
            Add(Build(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)), m.Index);

        // numeric day/month/year (day-first; swap if clearly US)
        foreach (Match m in Regex.Matches(hay, @"\b(\d{1,2})[.\-/](\d{1,2})[.\-/](\d{2,4})\b"))
        {
            int a = int.Parse(m.Groups[1].Value), b = int.Parse(m.Groups[2].Value);
            int y = Year(m.Groups[3].Value);
            (int day, int mon) = a > 12 && b <= 12 ? (a, b) : (b > 12 && a <= 12 ? (b, a) : (a, b));
            Add(Build(y, mon, day), m.Index);
        }

        // month-name + year only ("Nov 2024") — day defaults to 1
        foreach (Match m in Regex.Matches(hay, @"\b([A-Za-zĂÂÎăâî]{3,9})\.?\s+(\d{4})\b"))
            Add(FromParts("1", m.Groups[1].Value, m.Groups[2].Value), m.Index);

        if (cands.Count == 0) return null;

        // Prefer a date sitting just after an "expiry" label.
        foreach (var w in ExpiryWords)
        {
            int k = lower.IndexOf(w, StringComparison.Ordinal);
            while (k >= 0)
            {
                var near = cands.Where(c => c.Index >= k && c.Index <= k + 80)
                                .OrderBy(c => c.Index).FirstOrDefault();
                if (near is not null) return near.Date.ToString("yyyy-MM-dd");
                k = lower.IndexOf(w, k + 1, StringComparison.Ordinal);
            }
        }

        // Otherwise the latest plausible date — for IDs the expiry is later than issue.
        return cands.OrderByDescending(c => c.Date).First().Date.ToString("yyyy-MM-dd");
    }

    private static DateTime? FromParts(string dayStr, string monthWord, string yearStr)
    {
        int month = MonthOf(monthWord);
        if (month == 0) return null;
        if (!int.TryParse(dayStr, out var day)) return null;
        return Build(Year(yearStr), month, day);
    }

    private static int MonthOf(string word)
    {
        var w = word.Trim().ToLowerInvariant();
        // longest-prefix match so "mart"/"mai" beat a bare "mar"/"ma"
        int best = 0, bestLen = 0;
        foreach (var (prefix, month) in Months)
            if (w.StartsWith(prefix) && prefix.Length > bestLen) { best = month; bestLen = prefix.Length; }
        return best;
    }

    private static int Year(string s)
    {
        if (!int.TryParse(s, out var y)) return 0;
        return y < 100 ? 2000 + y : y;
    }

    private static DateTime? Build(int y, int m, int d)
    {
        if (y < 1900 || m < 1 || m > 12 || d < 1 || d > 31) return null;
        try { return new DateTime(y, m, d); } catch { return null; }
    }

    private static bool Plausible(DateTime d) =>
        d >= DateTime.Today.AddYears(-30) && d <= DateTime.Today.AddYears(50);
}
