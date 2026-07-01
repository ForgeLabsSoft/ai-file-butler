using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AIFileButler;

/// <summary>
/// Local text embeddings via Ollama, for semantic search over the user's own files.
/// Uses whatever model is configured (works out of the box with the chat model the
/// app already needs) — a dedicated embedder like "nomic-embed-text" is better if
/// pulled. Everything stays on-device; returns null if Ollama/model is unavailable.
/// </summary>
internal static class Embedder
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    /// <summary>Embed a piece of text into a unit-length vector, or null on failure.</summary>
    public static float[]? Embed(string text, string ollamaUrl, string model)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            var body = JsonSerializer.Serialize(new { model, prompt = Snip(text) });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp = Http.PostAsync($"{ollamaUrl.TrimEnd('/')}/api/embeddings", content).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode) return null;
            var json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("embedding", out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
            var v = new float[arr.GetArrayLength()];
            int i = 0;
            foreach (var e in arr.EnumerateArray()) v[i++] = (float)e.GetDouble();
            return v.Length > 0 ? Normalize(v) : null;
        }
        catch { return null; }
    }

    /// <summary>Cheap check that embeddings work with the current Ollama + model.</summary>
    public static bool Available(string ollamaUrl, string model) => Embed("hello", ollamaUrl, model) is not null;

    /// <summary>Cosine of two unit vectors = dot product. 0 if lengths differ.</summary>
    public static float Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;
        float s = 0;
        for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
        return s;
    }

    private static string Snip(string t) => t.Length > 3000 ? t[..3000] : t;

    private static float[] Normalize(float[] v)
    {
        double n = 0;
        foreach (var x in v) n += (double)x * x;
        n = Math.Sqrt(n);
        if (n <= 0) return v;
        for (int i = 0; i < v.Length; i++) v[i] = (float)(v[i] / n);
        return v;
    }
}
