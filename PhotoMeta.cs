using Windows.Graphics.Imaging;
using Windows.Media.FaceAnalysis;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AIFileButler;

/// <summary>Reads photo metadata for sorting: capture date and GPS location
/// (from EXIF), and whether the photo contains people (built-in face detector).</summary>
public static class PhotoMeta
{
    /// <summary>"yyyy-MM" from EXIF capture date, falling back to the file date.</summary>
    public static string DateFolder(FileInfo file)
    {
        var dt = ExifDate(file) ?? file.LastWriteTime;
        return dt.ToString("yyyy-MM");
    }

    /// <summary>Nearest city from EXIF GPS, or "Unknown Location" if no GPS.</summary>
    public static string LocationFolder(FileInfo file)
    {
        var (lat, lon) = ExifGps(file);
        return lat is double la && lon is double lo ? Geo.NearestCity(la, lo) : "Unknown Location";
    }

    /// <summary>Recognize who's in the photo: "People/&lt;Name&gt;" for an enrolled
    /// person, "People/Unknown" for an unrecognized face, or "" if no face.</summary>
    public static string PeopleFolder(FileInfo file)
    {
        var emb = FaceRecognizer.EmbedDominantFace(file.FullName);
        if (emb is null) return HasPeople(file) ? "People/Unknown" : "";
        var name = People.Identify(emb);
        return name is null ? "People/Unknown" : "People/" + name;
    }

    private static DateTime? ExifDate(FileInfo file)
    {
        try
        {
            using var f = TagLib.File.Create(file.FullName);
            if (f is TagLib.Image.File img && img.ImageTag.DateTime is DateTime dt) return dt;
        }
        catch { }
        return null;
    }

    private static (double?, double?) ExifGps(FileInfo file)
    {
        try
        {
            using var f = TagLib.File.Create(file.FullName);
            if (f is TagLib.Image.File img)
                return (img.ImageTag.Latitude, img.ImageTag.Longitude);
        }
        catch { }
        return (null, null);
    }

    private static bool HasPeople(FileInfo file)
    {
        try
        {
            var detector = FaceDetector.CreateAsync().GetAwaiter().GetResult();
            var sf = StorageFile.GetFileFromPathAsync(file.FullName).GetAwaiter().GetResult();
            using IRandomAccessStream stream = sf.OpenAsync(FileAccessMode.Read).GetAwaiter().GetResult();
            var decoder = BitmapDecoder.CreateAsync(stream).GetAwaiter().GetResult();
            using var bmp = decoder.GetSoftwareBitmapAsync().GetAwaiter().GetResult();

            // The face detector needs a specific format (usually Gray8).
            var fmt = FaceDetector.GetSupportedBitmapPixelFormats().Contains(BitmapPixelFormat.Gray8)
                ? BitmapPixelFormat.Gray8 : BitmapPixelFormat.Nv12;
            SoftwareBitmap? converted = bmp.BitmapPixelFormat == fmt ? null : SoftwareBitmap.Convert(bmp, fmt);
            try
            {
                var faces = detector.DetectFacesAsync(converted ?? bmp).GetAwaiter().GetResult();
                return faces.Count > 0;
            }
            finally { converted?.Dispose(); }
        }
        catch { return false; } // unreadable / unsupported — treat as no people
    }
}
