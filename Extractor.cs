using System.Text;
using UglyToad.PdfPig;
using Windows.Data.Pdf;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AIFileButler;

/// <summary>
/// Pulls a short text snippet from a file so the classifier can 'read' content.
/// Degrades gracefully: anything it can't parse falls back to the filename.
/// </summary>
public static class Extractor
{
    private static readonly HashSet<string> TextExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".csv", ".log", ".ini", ".json", ".xml", ".html",
        ".py", ".js", ".ts", ".cs", ".java", ".c", ".cpp", ".go", ".rs",
        ".sql", ".sh", ".ps1", ".yaml", ".yml", ".toml",
    };

    private static readonly HashSet<string> ImageExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff",
    };

    public static string Snippet(string path, int maxChars)
    {
        var ext = Path.GetExtension(path);
        try
        {
            if (TextExts.Contains(ext))
                return ReadHead(path, maxChars);
            if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                return ReadPdf(path, maxChars);
            if (ImageExts.Contains(ext))
                return ReadImageOcr(path, maxChars); // photo/scan of a receipt etc.
        }
        catch (Exception ex)
        {
            return $"[could not read content: {ex.Message}]";
        }
        return ""; // other binaries: filename is the only signal
    }

    private static string ReadHead(string path, int maxChars)
    {
        using var reader = new StreamReader(path);
        var buffer = new char[maxChars];
        int read = reader.Read(buffer, 0, maxChars);
        return new string(buffer, 0, read).Trim();
    }

    private static string ReadPdf(string path, int maxChars)
    {
        string text;
        using (var doc = UglyToad.PdfPig.PdfDocument.Open(path)) // PdfPig: text-layer extraction
        {
            var sb = new StringBuilder();
            foreach (var page in doc.GetPages())
            {
                sb.Append(page.Text).Append('\n');
                if (sb.Length >= maxChars) break;
            }
            text = sb.ToString().Trim();
        }
        // Enough real text? Use it. Otherwise it's likely a scanned/image-only
        // PDF — render the pages and OCR them.
        if (text.Length >= 16)
            return text.Length > maxChars ? text[..maxChars] : text;

        var ocr = ReadScannedPdfOcr(path, maxChars);
        return ocr.Length > 0 ? ocr : text;
    }

    /// <summary>OCR a scanned/image-only PDF by rendering pages with the
    /// built-in Windows PDF renderer (Windows.Data.Pdf) then running OCR.</summary>
    private static string ReadScannedPdfOcr(string path, int maxChars)
    {
        var engine = GetEngine();
        if (engine is null) return "";

        var file = StorageFile.GetFileFromPathAsync(path).GetAwaiter().GetResult();
        var pdf = Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file).GetAwaiter().GetResult();

        var sb = new StringBuilder();
        uint pages = Math.Min(pdf.PageCount, 3u); // first few pages are enough
        for (uint i = 0; i < pages; i++)
        {
            using var page = pdf.GetPage(i);
            using var stream = new InMemoryRandomAccessStream();
            var opts = new PdfPageRenderOptions { DestinationWidth = 1654 }; // ~200 DPI A4
            page.RenderToStreamAsync(stream, opts).GetAwaiter().GetResult();

            var decoder = BitmapDecoder.CreateAsync(stream).GetAwaiter().GetResult();
            using var bmp = decoder.GetSoftwareBitmapAsync().GetAwaiter().GetResult();
            sb.Append(Ocr(engine, bmp)).Append('\n');
            if (sb.Length >= maxChars) break;
        }
        var text = sb.ToString().Trim();
        return text.Length > maxChars ? text[..maxChars] : text;
    }

    /// <summary>
    /// OCR an image with the built-in Windows engine (no external binary).
    /// Returns "" if no OCR language pack is available or the image won't decode.
    /// </summary>
    private static string ReadImageOcr(string path, int maxChars)
    {
        var engine = GetEngine();
        if (engine is null) return "";

        var file = StorageFile.GetFileFromPathAsync(path).GetAwaiter().GetResult();
        using IRandomAccessStream stream = file.OpenAsync(FileAccessMode.Read).GetAwaiter().GetResult();
        var decoder = BitmapDecoder.CreateAsync(stream).GetAwaiter().GetResult();
        using var bitmap = decoder.GetSoftwareBitmapAsync().GetAwaiter().GetResult();

        var text = Ocr(engine, bitmap);
        return text.Length > maxChars ? text[..maxChars] : text;
    }

    private static OcrEngine? GetEngine() =>
        OcrEngine.TryCreateFromUserProfileLanguages()
        ?? OcrEngine.TryCreateFromLanguage(new Language("en"));

    private static string Ocr(OcrEngine engine, SoftwareBitmap bmp)
    {
        // OCR needs Bgra8; convert defensively (rendered/decoded formats vary).
        SoftwareBitmap? converted = bmp.BitmapPixelFormat == BitmapPixelFormat.Bgra8
            ? null : SoftwareBitmap.Convert(bmp, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        try
        {
            var result = engine.RecognizeAsync(converted ?? bmp).GetAwaiter().GetResult();
            return (result.Text ?? "").Trim();
        }
        finally { converted?.Dispose(); }
    }
}
