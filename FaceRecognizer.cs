using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Windows.Graphics.Imaging;
using Windows.Media.FaceAnalysis;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AIFileButler;

/// <summary>On-device face recognition with a small (~13 MB) MobileFaceNet model
/// (ArcFace, WebFace600K) via ONNX Runtime, plus the built-in Windows face
/// detector. Two photos of the same person give vectors with high cosine
/// similarity. Nothing leaves the device.</summary>
public static class FaceRecognizer
{
    private static readonly object _lock = new();
    private static InferenceSession? _session;
    private static string _input = "input.1";

    // The model is NOT bundled (its license is non-commercial-research-only, so we
    // can't redistribute it). It's downloaded once, on demand, to a local cache.
    private const string ModelUrl =
        "https://huggingface.co/immich-app/buffalo_s/resolve/main/recognition/model.onnx";

    public static string ModelPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AI File Butler", "models", "w600k_mbf.onnx");

    /// <summary>True once the face-recognition model has been downloaded.</summary>
    public static bool ModelReady => File.Exists(ModelPath);

    /// <summary>Download the ~13 MB model to the local cache. Returns success.</summary>
    public static bool DownloadModel()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ModelPath)!);
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var bytes = http.GetByteArrayAsync(ModelUrl).GetAwaiter().GetResult();
            if (bytes.Length < 1_000_000) return false; // sanity: real model is ~13 MB
            File.WriteAllText(ModelPath + ".tmp", ""); // ensure dir writable
            File.WriteAllBytes(ModelPath, bytes);
            File.Delete(ModelPath + ".tmp");
            return true;
        }
        catch { return false; }
    }

    private static bool Ensure()
    {
        if (_session is not null) return true;
        if (!ModelReady) return false; // not downloaded yet
        lock (_lock)
        {
            if (_session is not null) return true;
            try
            {
                _session = new InferenceSession(ModelPath);
                _input = _session.InputMetadata.Keys.First();
                return true;
            }
            catch { return false; }
        }
    }

    /// <summary>L2-normalized embedding of the largest face, or null if no face.</summary>
    /// <summary>Embedding of the largest face (used for enrolling a single person).</summary>
    public static float[]? EmbedDominantFace(string path)
    {
        var all = EmbedAllFaces(path);
        return all.Count > 0 ? all[0] : null; // boxes are largest-first
    }

    /// <summary>Embeddings of EVERY detected face (largest first), so a group photo
    /// can be matched to any enrolled person — not just whoever's face is biggest.</summary>
    public static List<float[]> EmbedAllFaces(string path)
    {
        var res = new List<float[]>();
        if (!Ensure()) return res;
        try
        {
            path = Path.GetFullPath(path); // WinRT StorageFile needs an absolute path
            var boxes = DetectAllFaces(path);
            if (boxes.Count == 0) return res;
            using var bmp = new Bitmap(path);
            foreach (var box in boxes)
            {
                var emb = EmbedCrop(bmp, Expand(box, bmp.Width, bmp.Height, 0.20f));
                if (emb is not null) res.Add(emb);
            }
        }
        catch { }
        return res;
    }

    private static float[]? EmbedCrop(Bitmap bmp, Rectangle r)
    {
        try
        {
            using var face = new Bitmap(112, 112);
            using (var g = Graphics.FromImage(face))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(bmp, new Rectangle(0, 0, 112, 112), r, GraphicsUnit.Pixel);
            }

            var input = new DenseTensor<float>(new[] { 1, 3, 112, 112 });
            for (int y = 0; y < 112; y++)
                for (int x = 0; x < 112; x++)
                {
                    var p = face.GetPixel(x, y);
                    input[0, 0, y, x] = (p.R - 127.5f) / 127.5f;
                    input[0, 1, y, x] = (p.G - 127.5f) / 127.5f;
                    input[0, 2, y, x] = (p.B - 127.5f) / 127.5f;
                }

            using var results = _session!.Run(new[] { NamedOnnxValue.CreateFromTensor(_input, input) });
            var emb = results.First().AsEnumerable<float>().ToArray();
            return emb.Length > 0 ? Normalize(emb) : null;
        }
        catch { return null; }
    }

    private static Rectangle Expand(Rectangle r, int w, int h, float frac)
    {
        int dx = (int)(r.Width * frac), dy = (int)(r.Height * frac);
        int x = Math.Max(0, r.X - dx), y = Math.Max(0, r.Y - dy);
        int rw = Math.Min(w - x, r.Width + 2 * dx), rh = Math.Min(h - y, r.Height + 2 * dy);
        return new Rectangle(x, y, Math.Max(1, rw), Math.Max(1, rh));
    }

    private static List<Rectangle> DetectAllFaces(string path)
    {
        var res = new List<Rectangle>();
        try
        {
            var detector = FaceDetector.CreateAsync().GetAwaiter().GetResult();
            var sf = StorageFile.GetFileFromPathAsync(path).GetAwaiter().GetResult();
            using IRandomAccessStream stream = sf.OpenAsync(FileAccessMode.Read).GetAwaiter().GetResult();
            var decoder = BitmapDecoder.CreateAsync(stream).GetAwaiter().GetResult();
            using var bmp = decoder.GetSoftwareBitmapAsync().GetAwaiter().GetResult();

            var fmt = FaceDetector.GetSupportedBitmapPixelFormats().Contains(BitmapPixelFormat.Gray8)
                ? BitmapPixelFormat.Gray8 : BitmapPixelFormat.Nv12;
            SoftwareBitmap? conv = bmp.BitmapPixelFormat == fmt ? null : SoftwareBitmap.Convert(bmp, fmt);
            IList<DetectedFace> faces;
            try { faces = detector.DetectFacesAsync(conv ?? bmp).GetAwaiter().GetResult(); }
            finally { conv?.Dispose(); }
            foreach (var f in faces.OrderByDescending(f => (long)f.FaceBox.Width * f.FaceBox.Height))
                res.Add(new Rectangle((int)f.FaceBox.X, (int)f.FaceBox.Y, (int)f.FaceBox.Width, (int)f.FaceBox.Height));
        }
        catch { }
        return res;
    }

    public static float[] Normalize(float[] v)
    {
        double sum = 0;
        foreach (var x in v) sum += x * x;
        var n = (float)Math.Sqrt(sum);
        if (n < 1e-9f) return v;
        var r = new float[v.Length];
        for (int i = 0; i < v.Length; i++) r[i] = v[i] / n;
        return r;
    }

    public static float Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length) return -1f;
        float dot = 0;
        for (int i = 0; i < a.Length; i++) dot += a[i] * b[i];
        return dot; // inputs are L2-normalized
    }
}
