namespace AIFileButler;

/// <summary>Echo-inspired reflection: resurface photos taken on this day in past
/// years, and a gentle summary of your library. All from the dates the Butler
/// already read — nothing leaves the device.</summary>
public static class Memories
{
    private static readonly HashSet<string> ImgExt = new(StringComparer.OrdinalIgnoreCase)
    { ".jpg", ".jpeg", ".png", ".heic", ".heif", ".webp", ".bmp", ".tif", ".tiff", ".gif" };

    public sealed class Mem
    {
        public string File = "";
        public DateTime Date;
        public int YearsAgo;
    }

    /// <summary>Photos taken on today's month/day in earlier years.</summary>
    public static List<Mem> OnThisDay(string destRoot)
    {
        var res = new List<Mem>();
        var imgRoot = Path.Combine(destRoot, "Images");
        if (!Directory.Exists(imgRoot)) return res;
        var today = DateTime.Today;
        foreach (var f in Safe(imgRoot))
        {
            if (!ImgExt.Contains(Path.GetExtension(f))) continue;
            DateTime d;
            try { d = PhotoMeta.CaptureDate(f); } catch { continue; }
            if (d.Month == today.Month && d.Day == today.Day && d.Year < today.Year)
                res.Add(new Mem { File = f, Date = d, YearsAgo = today.Year - d.Year });
        }
        return res.OrderByDescending(m => m.Date).ToList();
    }

    /// <summary>A small library summary: photos, documents, enrolled people.</summary>
    public static (int Photos, int Docs, int People) Summary(string destRoot)
    {
        int photos = CountFiles(Path.Combine(destRoot, "Images"), onlyImages: true);
        int docs = CountFiles(Path.Combine(destRoot, "Documents"))
                 + CountFiles(Path.Combine(destRoot, "Invoices"))
                 + CountFiles(Path.Combine(destRoot, "Receipts"));
        return (photos, docs, People.List().Count);
    }

    private static int CountFiles(string dir, bool onlyImages = false)
    {
        if (!Directory.Exists(dir)) return 0;
        var files = Safe(dir);
        if (onlyImages) files = files.Where(f => ImgExt.Contains(Path.GetExtension(f)));
        return files.Count();
    }

    private static IEnumerable<string> Safe(string dir)
    {
        try { return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories); }
        catch { return Enumerable.Empty<string>(); }
    }
}
