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

    /// <summary>A planned move plus the data needed to apply it later (so a preview
    /// can be shown and only the accepted moves applied).</summary>
    public sealed record Pending(Plan Plan, string Snippet, Suggestion Sug, FileInfo File);

    public List<Plan> ScanNow(bool apply)
    {
        lock (_lock)
        {
            // Notice files the user moved between categories and learn from it.
            foreach (var (token, cat) in _learner.DetectCorrections(_cfg.DestRoot))
                Notify?.Invoke(EventKind.Info, string.Format(L.S("n_learned"), token, cat));

            CheckExpiries();

            var moves = BuildMoves();
            if (apply) ApplyMoves(moves);
            return moves.Select(m => m.Plan).ToList();
        }
    }

    /// <summary>Plan the moves without applying them — for a preview the user confirms.</summary>
    public List<Pending> PreviewMoves()
    {
        lock (_lock) return BuildMoves();
    }

    // Add a filed document to the semantic search index in the background (best effort;
    // no-op if the embedding model isn't available).
    private void IndexDoc(string path, string folder, string snippet)
    {
        if (string.IsNullOrWhiteSpace(snippet)) return;
        var url = _cfg.OllamaUrl;
        var model = _cfg.EmbedModel;
        Task.Run(() =>
        {
            try { var v = Embedder.Embed(snippet, url, model); if (v is not null) SearchIndex.Add(path, folder, snippet, v, model); }
            catch { }
        });
    }

    private List<Pending> BuildMoves()
    {
        var clf = ClassifierFactory.Get(_cfg);
        Backend = clf.Backend;
        var moves = new List<Pending>();
        foreach (var dir in _cfg.WatchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Eligible(dir))
            {
                // One bad file (locked, too-long path, vanished) must never abort
                // the whole batch — log it and move on.
                try
                {
                    var snippet = Extractor.Snippet(file.FullName, _cfg.MaxContentChars);
                    var decided = Decide(file, snippet, clf);
                    if (decided is null) continue;
                    var sug = decided.Value;
                    if (sug.Category == "music") sug = MediaMeta.EnrichMusic(file, sug);
                    moves.Add(new Pending(Organizer.BuildPlan(file, sug, _cfg), snippet, sug, file));
                }
                catch (Exception ex)
                {
                    Notify?.Invoke(EventKind.Error, $"{file.Name}: {ex.Message}");
                }
            }
        }
        return moves;
    }

    /// <summary>Apply a (possibly user-filtered) set of planned moves.</summary>
    public void ApplyMoves(IEnumerable<Pending> moves)
    {
        lock (_lock)
        {
            var applied = new List<Plan>();
            foreach (var m in moves)
            {
                try
                {
                    Organizer.Apply(m.Plan);
                    _learner.RecordPlacement(m.Plan.Dst, m.File.Name, m.Plan.Category);
                    RecordExpiry(m.Plan.Dst, m.File, m.Snippet, m.Sug);
                    IndexDoc(m.Plan.Dst, Config.FolderFor(m.Plan.Category), m.Snippet);
                    _seenSizes.Remove(m.File.FullName);
                    SessionCount++;
                    applied.Add(m.Plan);
                }
                catch (Exception ex)
                {
                    Notify?.Invoke(EventKind.Error, $"{m.File.Name}: {ex.Message}");
                }
            }
            if (applied.Count > 0)
            {
                LastBatchSize = applied.Count;
                var names = string.Join(", ", applied.Take(3).Select(p => p.DstName));
                var extra = applied.Count > 3 ? $" +{applied.Count - 3} more" : "";
                Notify?.Invoke(EventKind.Organized, string.Format(L.S("n_organized"), applied.Count, names + extra));
            }
        }
    }

    /// <summary>Record an expiry reminder for a filed document. Uses the AI's
    /// answer if it gave one, otherwise falls back to a backend-independent
    /// regex scan of the document text — but only when expiry scanning is on.</summary>
    private void RecordExpiry(string dst, FileInfo file, string snippet, Suggestion sug)
    {
        if (!_cfg.ExpiryScan) return;

        var expiry = sug.Expiry;
        var kind = sug.DocKind;
        if (string.IsNullOrWhiteSpace(expiry))
        {
            var found = ExpiryScanner.Scan(file.Name, snippet);
            if (found is null) return;
            expiry = found.Date;
            if (string.IsNullOrWhiteSpace(kind)) kind = found.Kind;
            Reminders.Record(dst, kind ?? "", expiry!, found.Name, found.Country, found.Id);
            return;
        }
        Reminders.Record(dst, kind ?? "", expiry!);
    }

    /// <summary>Notify when a document crosses one of its reminder lead times
    /// (global default or a per-document override). Each threshold fires once.</summary>
    public void CheckExpiries()
    {
        Reminders.RollRecurring(); // advance any past-due weekly/monthly/yearly tasks
        foreach (var i in Reminders.All())
        {
            int d = Reminders.DaysLeft(i);
            var leads = Reminders.LeadFor(i, _cfg.ReminderDays);
            var crossed = leads.Where(L => d <= L && !i.Notified.Contains(L)).ToList();
            if (crossed.Count == 0) continue;

            Reminders.MarkNotified(i.File, crossed);
            string msg = i.Category == "task"
                ? string.Format(L.S(d <= 0 ? "n_task_today" : "n_task_due"), i.Label, d)
                : (d < 0 ? string.Format(L.S("n_expired"), i.Kind)
                         : string.Format(L.S("n_expiring"), i.Kind, d));
            Notify?.Invoke(EventKind.Info, msg);
        }
    }

    /// <summary>Decide where a file goes: user rule, then learned, then AI/rules.
    /// Returns null to skip (e.g. the classifier errored).</summary>
    private Suggestion? Decide(FileInfo file, string snippet, IClassifier clf)
    {
        var fname = Slug.Make(Path.GetFileNameWithoutExtension(file.Name), file.Extension);
        var rule = _cfg.EvaluateRules(file.Name, snippet, file.Extension);
        if (rule is not null)
        {
            if (rule.Action == "skip") return null; // leave the file where it is
            var folder = rule.Action == "review" ? "_Review" : rule.Folder;
            return new Suggestion("rule", fname, 0.95, $"rule: {rule.Field} {rule.Op} \"{rule.Match}\"", "rule") { FolderOverride = folder };
        }
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
                if (IsCloudOrLink(file)) continue;
                try
                {
                    var snippet = Extractor.Snippet(file.FullName, _cfg.MaxContentChars);
                    var decided = Decide(file, snippet, clf);
                    if (decided is null) continue;
                    var sug = decided.Value;
                    if (sug.Category == "music") sug = MediaMeta.EnrichMusic(file, sug);
                    var plan = Organizer.BuildPlan(file, sug, _cfg);
                    Organizer.Apply(plan);
                    _learner.RecordPlacement(plan.Dst, file.Name, plan.Category);
                    RecordExpiry(plan.Dst, file, snippet, sug);
                    SessionCount++;
                    plans.Add(plan);
                }
                catch (Exception ex)
                {
                    Notify?.Invoke(EventKind.Error, $"{file.Name}: {ex.Message}");
                }
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
            // Never touch cloud placeholders (OneDrive "online-only" would force a
            // multi-GB download) or junctions/symlinks (reparse points).
            if (IsCloudOrLink(fi)) continue;

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

    // OneDrive online-only placeholders (FILE_ATTRIBUTE_OFFLINE / RecallOnDataAccess)
    // and reparse points (junctions, symlinks) — moving these is unsafe.
    private static bool IsCloudOrLink(FileInfo fi)
    {
        try
        {
            const FileAttributes Offline = FileAttributes.Offline;
            const FileAttributes RecallOnAccess = (FileAttributes)0x400000; // FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS
            var a = fi.Attributes;
            return (a & (Offline | RecallOnAccess | FileAttributes.ReparsePoint)) != 0;
        }
        catch { return false; }
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
