using System.Globalization;
using System.Text.RegularExpressions;

namespace AIFileButler;

/// <summary>
/// Parser for the Machine-Readable Zone (the &lt;&lt;&lt;-filled lines at the bottom
/// of travel documents). It follows ICAO Doc 9303 field positions, which are the
/// SAME for every country — so this recognises passports (TD3), visas (MRV),
/// and ID cards (TD1/TD2) worldwide. Tolerant of OCR noise: spaces and stray
/// punctuation are folded into the filler character '&lt;'.
/// Refs: ICAO 9303 · en.wikipedia.org/wiki/Machine-readable_passport
/// </summary>
public static class Mrz
{
    public sealed record Data(string DocType, string Country, string Name, string Number,
                              string Nationality, string Expiry);

    // A TD3/TD2 second line: docNo(9) check(1) nat(3) DOB(6) check(1) sex(1) expiry(6) check(1)…
    private static readonly Regex DataLine =
        new(@"^[A-Z0-9<]{9}[0-9<][A-Z<]{3}[0-9<]{6}[0-9<][MFX<][0-9<]{6}[0-9<]", RegexOptions.Compiled);

    public static Data? TryParse(string text)
    {
        var lines = PackedLines(text);
        if (lines.Count == 0) return null;

        // --- TD3 (passport) / TD2 / MRV (visa): two lines, name line + data line ---
        int di = lines.FindIndex(l => l.Length >= 28 && DataLine.IsMatch(l));
        if (di >= 0)
        {
            var data = lines[di];
            string? nameL = PickNameLine(lines, di);
            string docType = "", country = "";
            string name = "";
            if (nameL is not null)
            {
                docType = nameL.Length > 0 ? nameL[0].ToString() : "";
                if (nameL.Length >= 5 && IsAlpha(nameL.Substring(2, 3))) country = nameL.Substring(2, 3);
                name = NameFromBlob(nameL.Length >= 5 ? nameL[5..] : nameL);
            }
            string number = Trim(data[..9]);
            string nationality = data.Length >= 13 ? Letters(data.Substring(10, 3)) : "";
            string expiry = DateFrom(data.Length >= 28 ? data.Substring(21, 6) : "");
            if (country.Length == 0) country = nationality;
            if (expiry.Length == 0) return null;
            return new Data(docType, country, name, number, nationality, expiry);
        }

        // --- TD1 (ID card): three 30-char lines (name on line 3) ---
        var td1 = TryTd1(lines);
        if (td1 is not null) return td1;

        return null;
    }

    private static Data? TryTd1(List<string> lines)
    {
        // line1: type(1) td(1) country(3) docno(9) check(1) …
        int i1 = lines.FindIndex(l => l.Length >= 28 && Regex.IsMatch(l, @"^[IAC][A-Z0-9<][A-Z<]{3}[A-Z0-9<]{9}"));
        if (i1 < 0 || i1 + 2 >= lines.Count) return null;
        var l1 = lines[i1];
        var l2 = lines[i1 + 1];
        var l3 = lines[i1 + 2];
        if (!l3.Contains("<<") && !l3.Contains('<')) return null;

        string docType = l1[0].ToString();
        string country = IsAlpha(l1.Substring(2, 3)) ? l1.Substring(2, 3) : "";
        string number = Trim(l1.Substring(5, 9));
        // line2: DOB(6) check(1) sex(1) expiry(6) check(1) nationality(3) …
        string expiry = l2.Length >= 14 ? DateFrom(l2.Substring(8, 6)) : "";
        string nationality = l2.Length >= 18 ? Letters(l2.Substring(15, 3)) : "";
        if (country.Length == 0) country = nationality;
        string name = NameFromBlob(l3);
        if (expiry.Length == 0) return null;
        return new Data(docType, country, name, number, nationality, expiry);
    }

    // --- helpers ---------------------------------------------------------

    private static List<string> PackedLines(string text)
    {
        var res = new List<string>();
        foreach (var raw in text.Split('\n', '\r'))
        {
            // fold spaces + stray glyphs into the MRZ filler, keep letters/digits
            var packed = Regex.Replace(raw.ToUpperInvariant(), @"\s", "");
            packed = Regex.Replace(packed, @"[^A-Z0-9<]", "<");
            if (packed.Length is >= 28 and <= 46 && (packed.Contains('<') || char.IsDigit(packed[^1])))
                res.Add(packed);
        }
        return res;
    }

    private static string? PickNameLine(List<string> lines, int dataIdx)
    {
        // prefer the line just above the data line; otherwise any line with "<<"
        if (dataIdx - 1 >= 0 && lines[dataIdx - 1].Contains("<<")) return lines[dataIdx - 1];
        return lines.FirstOrDefault(l => l.Contains("<<"));
    }

    private static string NameFromBlob(string blob)
    {
        var parts = blob.Split(new[] { "<<" }, StringSplitOptions.None);
        string surname = Clean(parts[0]);
        string given = parts.Length > 1 ? Clean(parts[1]) : "";
        return TitleCase($"{given} {surname}".Trim());
    }

    private static string Clean(string mrz)
    {
        var s = mrz.Replace('<', ' ');
        s = Regex.Replace(s, @"\s{2,}.*$", "").Trim(); // drop filler / next-field bleed
        return s;
    }

    private static string DateFrom(string yymmdd)
    {
        var s = yymmdd.Replace('<', '0');
        if (s.Length != 6 || !int.TryParse(s, out _)) return "";
        if (!int.TryParse(s[..2], out var yy) || !int.TryParse(s.Substring(2, 2), out var mm) || !int.TryParse(s.Substring(4, 2), out var dd))
            return "";
        int year = 2000 + yy; // expiry is in the future for any live document
        if (mm is < 1 or > 12 || dd is < 1 or > 31) return "";
        try { return new DateTime(year, mm, dd).ToString("yyyy-MM-dd"); } catch { return ""; }
    }

    private static string Trim(string s) => s.Replace("<", "").Trim();
    private static string Letters(string s) => Regex.Replace(s, "[^A-Z]", "");
    private static bool IsAlpha(string s) => s.Length == 3 && Letters(s).Length == 3;

    private static string TitleCase(string s) =>
        s.Length == 0 ? s : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
}
