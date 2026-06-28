using System.Drawing;
using FaceONNX;

namespace AIFileButler;

/// <summary>Computes a numeric "fingerprint" (embedding) for the dominant face in
/// a photo, using the local FaceONNX models. Two photos of the same person yield
/// close vectors (high cosine similarity). All on-device, no cloud.</summary>
public static class FaceRecognizer
{
    private static readonly object _lock = new();
    private static FaceDetector? _detector;
    private static Face68LandmarksExtractor? _landmarks;
    private static FaceEmbedder? _embedder;
    private static bool _failed;

    private static bool Ensure()
    {
        if (_failed) return false;
        if (_embedder is not null) return true;
        lock (_lock)
        {
            if (_embedder is not null) return true;
            try
            {
                _detector = new FaceDetector();
                _landmarks = new Face68LandmarksExtractor();
                _embedder = new FaceEmbedder();
                return true;
            }
            catch { _failed = true; return false; }
        }
    }

    /// <summary>L2-normalized embedding of the largest face, or null if no face.</summary>
    public static float[]? EmbedDominantFace(string path)
    {
        if (!Ensure()) return null;
        try
        {
            using var bmp = new Bitmap(path);
            var faces = _detector!.Forward(bmp);
            if (faces is null || faces.Length == 0) return null;

            var best = faces.OrderByDescending(f => f.Box.Width * f.Box.Height).First();
            var lm = _landmarks!.Forward(bmp, best.Box);
            using var aligned = bmp.Align(best.Box, lm.RotationAngle, true);
            var emb = _embedder!.Forward(aligned);
            return emb is { Length: > 0 } ? Normalize(emb) : null;
        }
        catch { return null; }
    }

    public static float[] Normalize(float[] v)
    {
        double sum = 0;
        foreach (var x in v) sum += x * x;
        var norm = (float)Math.Sqrt(sum);
        if (norm < 1e-9f) return v;
        var r = new float[v.Length];
        for (int i = 0; i < v.Length; i++) r[i] = v[i] / norm;
        return r;
    }

    public static float Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length) return -1f;
        float dot = 0;
        for (int i = 0; i < a.Length; i++) dot += a[i] * b[i];
        return dot; // inputs are L2-normalized, so dot == cosine similarity
    }
}
