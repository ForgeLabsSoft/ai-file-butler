namespace AIFileButler;

public enum EventKind { Organized, Undo, Error, Info }

/// <summary>Background engine: polls watched folders and organizes settled files.</summary>
public sealed class Watcher
{
    private readonly Config _cfg;
    public event Action<EventKind, string>? Notify;

    public bool Auto { get; set; } = true;
    public bool Paused { get; set; } = false;
    public int SessionCount { get; private set; }
    public int LastBatchSize { get; private set; }
    public string Backend { get; private set; } = "rules";

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private readonly object _lock = new();
    private readonly Learner _learner = new();
    // path -> size for write-stability detection
    private readonly Dictionary<string, long> _seenSizes = new();

    public Watcher(Config cfg) => _cfg = cfg;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _loop?.Wait(3000); } catch { /* ignore */ }
    }

    private async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (!Paused)
            {
                try { ScanNow(apply: Auto); }
                catch (Exception ex) { Notify?.Invoke(EventKind.Error, $"scan failed: {ex.Message}"); }
            }
            try { await Task.Delay(TimeSpan.FromSeconds(_cfg.PollIntervalSeconds), token); }
            catch (TaskCanceledException) { break; }
        }
    }

    public List<Plan> ScanNow(bool apply)
    {
        lock (_lock)
        {
            var clf = ClassifierFactory.Get(_cfg);
            Backend = clf.Backend;
            var plans = new List<Plan>();

            // Notice files the user moved between categories and learn from it.
            foreach (var (token, cat) in _learner.DetectCorrections(_cfg.DestRoot))
                Notify?.Invoke(EventKind.Info, string.Format(L.S("n_learned"), token, cat));

            foreach (var dir in _cfg.WatchDirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Eligible(dir))
                {
                    var snippet = Extractor.Snippet(file.FullName, _cfg.MaxContentChars);
                    var decided = Decide(file, snippet, clf);
                    if (decided is null) continue;
                    var sug = decided.Value;
                    if (sug.Category == "music") sug = MediaMeta.EnrichMusic(file, sug);
                    var plan = Organizer.BuildPlan(file, sug, _cfg);
                    plans.Add(plan);
                    if (apply)
                    {
                        Organizer.Apply(plan);
                        _learner.RecordPlacement(plan.Dst, file.Name, plan.Category);
                        _seenSizes.Remove(file.FullName);
                        SessionCount++;
                    }
                }
            }

            if (apply && plans.Count > 0)
            {
                LastBatchSize = plans.Count;
                var names = string.Join(", ", plans.Take(3).Select(p => p.DstName));
                var extra = plans.Count > 3 ? $" +{plans.Count - 3} more" : "";
                Notify?.Invoke(EventKind.Organized, string.Format(L.S("n_organized"), plans.Count, names + extra));
            }
            return plans;
        }
    }

    /// <summary>Decide where a file goes: user rule, then learned, then AI/rules.
    /// Returns null to skip (e.g. the classifier errored).</summary>
    private Suggestion? Decide(FileInfo file, string snippet, IClassifier clf)
    {
        var fname = Slug.Make(Path.GetFileNameWithoutExtension(file.Name), file.Extension);
        var rule = _cfg.MatchRule(file.Name + "\n" + snippet);
        if (rule is not null)
            return new Suggestion("rule", fname, 0.95, $"your rule: \"{rule.Match}\"", "rule") { FolderOverride = rule.Folder };
        var learned = _learner.Suggest(file.Name);
        if (learned is not null)
            return new Suggestion(learned, fname, 0.95, "learned from your correction", "learned");
        try { return clf.Classify(file, snippet); }
        catch (Exception ex) { Notify?.Invoke(EventKind.Error, $"{file.Name}: {ex.Message}"); return null; }
    }

    /// <summary>Organize an explicit set of files now (e.g. drag-and-dropped),
    /// regardless of folder/age settings. Runs on the calling thread.</summary>
    public void OrganizeDropped(IEnumerable<string> paths)
    {
        lock (_lock)
        {
            var clf = ClassifierFactory.Get(_cfg);
            Backend = clf.Backend;
            var plans = new List<Plan>();
            foreach (var p in paths)
            {
                if (!File.Exists(p)) continue;
                var file = new FileInfo(p);
                if (_cfg.IgnoreExts.Contains(file.Extension, StringComparer.OrdinalIgnoreCase)) continue;
                var snippet = Extractor.Snippet(file.FullName, _cfg.MaxContentChars);
                var decided = Decide(file, snippet, clf);
                if (decided is null) continue;
                var sug = decided.Value;
                if (sug.Category == "music") sug = MediaMeta.EnrichMusic(file, sug);
                var plan = Organizer.BuildPlan(file, sug, _cfg);
                Organizer.Apply(plan);
                _learner.RecordPlacement(plan.Dst, file.Name, plan.Category);
                SessionCount++;
                plans.Add(plan);
            }
            if (plans.Count > 0)
            {
                LastBatchSize = plans.Count;
                var names = string.Join(", ", plans.Take(3).Select(p => p.DstName));
                var extra = plans.Count > 3 ? $" +{plans.Count - 3} more" : "";
                Notify?.Invoke(EventKind.Organized, string.Format(L.S("n_organized"), plans.Count, names + extra));
            }
        }
    }

    private IEnumerable<FileInfo> Eligible(string dir)
    {
        var now = DateTime.UtcNow;
        foreach (var path in Directory.EnumerateFiles(dir).OrderBy(p => p))
        {
            var fi = new FileInfo(path);
            if (_cfg.IgnoreExts.Contains(fi.Extension, StringComparer.OrdinalIgnoreCase)) continue;

            long size;
            try { size = fi.Length; } catch { continue; }

            bool seenBefore = _seenSizes.TryGetValue(path, out var prevSize);
            _seenSizes[path] = size;

            if ((now - fi.LastWriteTimeUtc).TotalSeconds < _cfg.MinAgeSeconds) continue;
            // First sighting of an already-old file counts as stable, so a
            // manual "Organize now" works on the first pass.
            if (!seenBefore || prevSize == size) yield return fi;
        }
    }

    public int UndoLast()
    {
        int n = Organizer.UndoLast(LastBatchSize > 0 ? LastBatchSize : 1);
        LastBatchSize = 0;
        SessionCount = Math.Max(0, SessionCount - n);
        Notify?.Invoke(EventKind.Undo, string.Format(L.S("n_reverted"), n));
        return n;
    }
}
