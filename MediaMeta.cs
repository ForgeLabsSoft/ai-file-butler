using System.Text.RegularExpressions;

namespace AIFileButler;

/// <summary>Reads embedded music tags so songs can be sorted by real metadata
/// (artist/genre/year) rather than a guess. Falls back silently if unreadable.</summary>
public static class MediaMeta
{
    /// <summary>Enrich a music suggestion with ID3/tag data (more reliable than the AI).</summary>
    public static Suggestion EnrichMusic(FileInfo file, Suggestion s)
    {
        try
        {
            using var tag = TagLib.File.Create(file.FullName);
            var artist = First(tag.Tag.Performers) ?? tag.Tag.FirstAlbumArtist;
            var genre = First(tag.Tag.Genres);
            var year = tag.Tag.Year > 0 ? tag.Tag.Year.ToString() : null;

            return s with
            {
                Person = NonEmpty(Clean(artist), s.Person),
                Genre = NonEmpty(Clean(genre), s.Genre),
                Year = NonEmpty(year, s.Year),
            };
        }
        catch { return s; } // unreadable/corrupt tag — keep what we have
    }

    private static string? First(string[]? a) => a is { Length: > 0 } ? a[0] : null;

    private static string? Clean(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        v = Regex.Replace(v, @"[^\w\- ]", "").Trim();
        return string.IsNullOrEmpty(v) ? null : (v.Length > 40 ? v[..40] : v);
    }

    private static string? NonEmpty(string? preferred, string? fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
}
