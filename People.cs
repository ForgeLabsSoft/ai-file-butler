using System.Text.Json;

namespace AIFileButler;

/// <summary>The user's enrolled people: a name plus one or more face embeddings.
/// A new photo's face is matched to the closest enrolled person by cosine
/// similarity. Stored locally in people.json — nothing leaves the device.</summary>
public static class People
{
    public sealed class Person
    {
        public string Name { get; set; } = "";
        public List<float[]> Embeddings { get; set; } = new();
    }

    /// <summary>Cosine-similarity threshold to count a face as a known person.</summary>
    public static float Threshold = 0.45f;

    private static readonly string Path_ =
        System.IO.Path.Combine(AppContext.BaseDirectory, "people.json");

    private static List<Person>? _cache;

    private static List<Person> Load()
    {
        if (_cache is not null) return _cache;
        try
        {
            if (File.Exists(Path_))
                _cache = JsonSerializer.Deserialize<List<Person>>(File.ReadAllText(Path_)) ?? new();
            else _cache = new();
        }
        catch { _cache = new(); }
        return _cache;
    }

    private static void Save() =>
        File.WriteAllText(Path_, JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = false }));

    public static List<(string Name, int Count)> List() =>
        Load().Select(p => (p.Name, p.Embeddings.Count)).ToList();

    public static void Enroll(string name, float[] embedding)
    {
        name = name.Trim();
        if (name.Length == 0) return;
        var list = Load();
        var p = list.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (p is null) { p = new Person { Name = name }; list.Add(p); }
        p.Embeddings.Add(embedding);
        Save();
    }

    public static void Remove(string name)
    {
        var list = Load();
        list.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    /// <summary>Best-matching enrolled person for a face, or null if none is close enough.</summary>
    public static string? Identify(float[] embedding)
    {
        string? best = null;
        float bestSim = Threshold;
        foreach (var p in Load())
            foreach (var e in p.Embeddings)
            {
                var sim = FaceRecognizer.Cosine(embedding, e);
                if (sim >= bestSim) { bestSim = sim; best = p.Name; }
            }
        return best;
    }
}
