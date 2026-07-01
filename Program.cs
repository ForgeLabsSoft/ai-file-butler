using System.Drawing;
using System.Windows.Forms;

namespace AIFileButler;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Headless hooks an installer/script can call:
        //   AIFileButler.exe --register     (enable start-with-Windows)
        //   AIFileButler.exe --unregister   (disable it)
        if (args.Length > 0)
        {
            switch (args[0].ToLowerInvariant())
            {
                case "--register": Startup.Set(true); return;
                case "--unregister": Startup.Set(false); return;
                case "--searchtest": // hidden QA: rebuild index from a folder + run a query
                {
                    var c = Config.Load();
                    int n = SearchIndex.Rebuild(args[1], c.OllamaUrl, c.EmbedModel);
                    var sb = new System.Text.StringBuilder($"indexed {n}\nquery: {args[2]}\n");
                    var qv = Embedder.Embed(args[2], c.OllamaUrl, c.EmbedModel);
                    if (qv is not null)
                        foreach (var (e, s) in SearchIndex.Query(qv, 5))
                            sb.AppendLine($"{s:F3}  {System.IO.Path.GetFileName(e.Path)}");
                    System.IO.File.WriteAllText("search-result.txt", sb.ToString());
                    return;
                }
                case "--embtest": // hidden QA: verify embeddings + semantic similarity
                {
                    var c = Config.Load();
                    var a = Embedder.Embed("passport renewal and travel document expiry", c.OllamaUrl, c.EmbedModel);
                    var b = Embedder.Embed("how do I renew my passport before it expires", c.OllamaUrl, c.EmbedModel);
                    var d = Embedder.Embed("chocolate cake recipe with eggs and flour", c.OllamaUrl, c.EmbedModel);
                    string r = a is null ? "embeddings unavailable"
                        : $"dims={a.Length}\nsim(passport, renew-passport)={Embedder.Cosine(a, b):F3}\nsim(passport, cake)={Embedder.Cosine(a, d):F3}";
                    System.IO.File.WriteAllText("emb-result.txt", r);
                    return;
                }
                case "--expiry": // hidden QA: test the expiry scanner on a text/file
                {
                    var input = args.Length >= 2 ? string.Join(" ", args.Skip(1)) : "";
                    var text = System.IO.File.Exists(input) ? Extractor.Snippet(input, 4000) : input;
                    var res = ExpiryScanner.Scan(System.IO.Path.GetFileName(input), text);
                    System.IO.File.WriteAllText("expiry-result.txt",
                        res is null ? "none" : $"{res.Kind} | {res.Date} | id='{res.Id}' | name='{res.Name}' | country='{res.Country}'");
                    return;
                }
                case "--downloadmodel": // hidden: fetch the face model into the cache
                    System.IO.File.WriteAllText("model-dl.txt", FaceRecognizer.DownloadModel() ? "ok" : "fail");
                    return;
                case "--enroll": // hidden: enroll a person from a photo (CLI/scripting)
                    if (args.Length >= 3)
                    {
                        var emb = FaceRecognizer.EmbedDominantFace(args[2]);
                        if (emb is not null) People.Enroll(args[1], emb);
                    }
                    return;
                case "--faces": // hidden QA: pairwise face cosine similarities -> faces-result.txt
                {
                    var imgs = args.Skip(1).ToArray();
                    var embs = imgs.Select(FaceRecognizer.EmbedDominantFace).ToArray();
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < imgs.Length; i++)
                        sb.AppendLine($"{System.IO.Path.GetFileName(imgs[i])}: face={(embs[i] != null)}");
                    for (int i = 0; i < imgs.Length; i++)
                        for (int j = i + 1; j < imgs.Length; j++)
                            if (embs[i] != null && embs[j] != null)
                                sb.AppendLine($"cos({System.IO.Path.GetFileName(imgs[i])},{System.IO.Path.GetFileName(imgs[j])}) = {FaceRecognizer.Cosine(embs[i]!, embs[j]!):F3}");
                    System.IO.File.WriteAllText("faces-result.txt", sb.ToString());
                    return;
                }
                case "--settings":
                    ApplicationConfiguration.Initialize();
                    var cfg = Config.Load();
                    Application.Run(new SettingsForm(cfg, new Watcher(cfg)));
                    return;
                case "--shot": // hidden: render a window to a PNG (docs/QA)
                    ApplicationConfiguration.Initialize();
                    var scfg = Config.Load();
                    var lang = args.Length > 1 ? args[1] : "en";
                    scfg.Language = lang; L.Lang = lang;
                    Theme.IsDark = scfg.DarkMode || (args.Length > 4 && args[4] == "dark");
                    Form sf = args.Length > 3 && args[3] == "welcome" ? new WelcomeForm()
                        : args.Length > 3 && args[3] == "history" ? new HistoryForm()
                        : args.Length > 3 && args[3] == "people" ? new PeopleForm()
                        : args.Length > 3 && args[3] == "memories" ? new MemoriesForm(scfg.DestRoot)
                        : args.Length > 3 && args[3] == "reminders" ? new ExpiryForm()
                        : args.Length > 3 && args[3] == "main" ? new MainWindow(scfg, new Watcher(scfg))
                        : new SettingsForm(scfg, new Watcher(scfg));
                    sf.Show();
                    // for the main window, optionally jump to a page: --shot ro out.png main <page>
                    if (sf is MainWindow mw && args.Length > 4 && args[4] != "dark")
                        mw.Open(args[4]);
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(300);
                    Application.DoEvents();
                    using (var bmp = new System.Drawing.Bitmap(sf.Width, sf.Height))
                    {
                        sf.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, sf.Width, sf.Height));
                        bmp.Save(args.Length > 2 ? args[2] : "settings-shot.png");
                    }
                    return;
            }
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new ButlerContext());
    }
}

/// <summary>The tray app: a NotifyIcon with a right-click menu, driven by a Watcher.</summary>
internal sealed class ButlerContext : ApplicationContext
{
    private readonly Config _cfg;
    private readonly Watcher _watcher;
    private readonly NotifyIcon _tray;
    private readonly SynchronizationContext _ui;

    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _watchItem;
    private readonly ToolStripMenuItem _autoItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _openMainItem;
    private readonly ToolStripMenuItem _organizeItem;
    private readonly ToolStripMenuItem _quitItem;
    private MainWindow? _main;

    public ButlerContext()
    {
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();
        _cfg = Config.Load();
        L.Lang = string.IsNullOrEmpty(_cfg.Language) ? "en" : _cfg.Language;
        Theme.IsDark = _cfg.DarkMode;
        People.Threshold = (float)_cfg.FaceThreshold;
        _watcher = new Watcher(_cfg);
        _watcher.Notify += OnWatcherEvent;
        // Safety: on the very first run, stay in manual mode (don't auto-move)
        // until the user opts in via the welcome screen.
        _watcher.Auto = _cfg.FirstRunDone && _cfg.AutoOrganize;

        _statusItem = new ToolStripMenuItem { Enabled = false };
        _watchItem = new ToolStripMenuItem { Enabled = false };
        _autoItem = new ToolStripMenuItem("", null, (_, _) => ToggleAuto());
        _pauseItem = new ToolStripMenuItem("", null, (_, _) => _watcher.Paused = !_watcher.Paused);
        _startupItem = new ToolStripMenuItem("", null, (_, _) => ToggleStartup());
        _openMainItem = new ToolStripMenuItem("", null, (_, _) => OpenMain())
            { Font = new Font(System.Drawing.SystemFonts.MenuFont!, System.Drawing.FontStyle.Bold) };
        _organizeItem = new ToolStripMenuItem("", null, (_, _) => OrganizeNow());
        _quitItem = new ToolStripMenuItem("", null, (_, _) => Quit());

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _statusItem,
            _watchItem,
            new ToolStripSeparator(),
            _openMainItem,
            new ToolStripSeparator(),
            _organizeItem,
            _autoItem,
            _pauseItem,
            new ToolStripSeparator(),
            _startupItem,
            _quitItem,
        });
        Theme.StyleMenu(menu);
        menu.Opening += (_, _) => RefreshMenu();

        _tray = new NotifyIcon
        {
            Icon = MakeIcon(),
            Text = "AI File Butler",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => OpenMain();

        _watcher.Start();
        RefreshMenu();

        if (!_cfg.FirstRunDone)
        {
            _cfg.FirstRunDone = true;
            _cfg.AutoOrganize = false; // stay manual until the user opts in
            _cfg.Save();
            ShowWelcome();
        }
        else
        {
            // A warm "on this day" greeting if there are memories, else a quiet hello.
            var mems = Memories.OnThisDay(_cfg.DestRoot);
            if (mems.Count > 0)
                _tray.ShowBalloonTip(6000, "AI File Butler",
                    string.Format(L.S("n_onthisday"), mems[0].YearsAgo, mems.Count), ToolTipIcon.Info);
            else
                _tray.ShowBalloonTip(4000, "AI File Butler", L.S("n_running"), ToolTipIcon.Info);
        }
    }

    private void ToggleAuto()
    {
        _watcher.Auto = !_watcher.Auto;
        _cfg.AutoOrganize = _watcher.Auto;
        try { _cfg.Save(); } catch { }
    }

    private void ShowWelcome()
    {
        using var w = new WelcomeForm();
        w.ShowDialog();
        switch (w.Choice)
        {
            case WelcomeChoice.OpenSettings:
                OpenMain("settings");
                break;
            case WelcomeChoice.EnableAuto:
                _watcher.Auto = true;
                _cfg.AutoOrganize = true;
                try { _cfg.Save(); } catch { }
                RefreshMenu();
                _tray.ShowBalloonTip(3000, "AI File Butler", L.S("n_startup_on"), ToolTipIcon.Info);
                break;
            // KeepManual: leave Auto off — nothing moves until the user enables it.
        }
    }

    private void RefreshMenu()
    {
        var mode = _watcher.Backend == "ollama" ? "AI (Ollama)" : "rules-only";
        var state = _watcher.Paused ? "paused" : (_watcher.Auto ? "auto" : "manual");
        _statusItem.Text = $"● {mode} · {state} · {_watcher.SessionCount}";

        var first = _cfg.WatchDirs.Count > 0 ? Path.GetFileName(_cfg.WatchDirs[0].TrimEnd('\\')) : "?";
        var more = _cfg.WatchDirs.Count > 1 ? $" +{_cfg.WatchDirs.Count - 1}" : "";
        _watchItem.Text = $"{first}{more}";

        // labels follow the language chosen in Settings; an emoji prefix makes
        // each row scannable at a glance (⏰ Reminders stands out, etc.)
        _openMainItem.Text = "🪟  " + L.S("m_open_window");
        _organizeItem.Text = "✨  " + L.S("m_organize");
        _autoItem.Text = "🔄  " + L.S("m_auto");
        _pauseItem.Text = "⏸  " + L.S("m_pause");
        _startupItem.Text = "🚀  " + L.S("startup");
        _quitItem.Text = "✖  " + L.S("m_quit");

        _autoItem.Checked = _watcher.Auto;
        _pauseItem.Checked = _watcher.Paused;
        _startupItem.Checked = Startup.IsEnabled;
    }

    private void ToggleStartup()
    {
        try
        {
            Startup.Set(!Startup.IsEnabled);
            _tray.ShowBalloonTip(3000, "AI File Butler",
                L.S(Startup.IsEnabled ? "n_startup_on" : "n_startup_off"), ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _tray.ShowBalloonTip(4000, "Butler error",
                $"Couldn't change startup setting: {ex.Message}", ToolTipIcon.Warning);
        }
    }

    private void OpenMain(string? page = null)
    {
        if (_main is null || _main.IsDisposed)
        {
            _main = new MainWindow(_cfg, _watcher);
            // when the user saves Settings, mirror the new state into the tray/watcher
            _main.SettingsChanged += () =>
            {
                _watcher.Auto = _cfg.AutoOrganize;
                People.Threshold = (float)_cfg.FaceThreshold;
                RefreshMenu();
            };
        }
        _main.Open(page);
    }

    private void OrganizeNow()
    {
        // plan off-thread, then let the user preview, then apply only what they keep
        Task.Run(() => { try { return _watcher.PreviewMoves(); } catch { return new List<Watcher.Pending>(); } })
            .ContinueWith(t => _ui.Post(_ =>
            {
                var moves = t.Result;
                if (moves.Count == 0) { _tray.ShowBalloonTip(3000, "AI File Butler", L.S("n_nothing"), ToolTipIcon.Info); return; }
                using var dlg = new PreviewForm(moves);
                if (dlg.ShowDialog() == DialogResult.OK && dlg.Accepted.Count > 0)
                    Task.Run(() => _watcher.ApplyMoves(dlg.Accepted));
            }, null));
    }

    private void OpenSorted()
    {
        Directory.CreateDirectory(_cfg.DestRoot);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = _cfg.DestRoot,
            UseShellExecute = true,
        });
    }

    private void OnWatcherEvent(EventKind kind, string message)
    {
        // Marshal to the UI thread — the watcher fires from a background task.
        _ui.Post(_ =>
        {
            var (title, icon) = kind switch
            {
                EventKind.Organized => ("AI File Butler", ToolTipIcon.Info),
                EventKind.Undo => (L.S("t_reverted"), ToolTipIcon.Info),
                EventKind.Error => (L.S("t_error"), ToolTipIcon.Warning),
                _ => ("AI File Butler", ToolTipIcon.Info),
            };
            _tray.ShowBalloonTip(5000, title, message, icon);
            RefreshMenu();
        }, null);
    }

    private void Quit()
    {
        _watcher.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        ExitThread();
    }

    private static Icon MakeIcon() => AppArt.Load();
}

/// <summary>Loads the app's embedded .ico for the tray, window and taskbar.</summary>
internal static class AppArt
{
    public static Icon Load()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                var ico = Icon.ExtractAssociatedIcon(path);
                if (ico is not null) return ico;
            }
        }
        catch { /* fall through to the drawn badge */ }
        return DrawIcon();
    }

    /// <summary>Fallback: a small round butler badge if the .ico can't load.</summary>
    private static Icon DrawIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var blue = new SolidBrush(Color.FromArgb(34, 139, 230));
            using var white = new SolidBrush(Color.White);
            using var gold = new SolidBrush(Color.FromArgb(255, 220, 0));
            g.FillEllipse(blue, 1, 1, 30, 30);
            g.FillEllipse(white, 12, 6, 8, 8);
            g.FillPolygon(white, new[] { new Point(10, 26), new Point(16, 20), new Point(22, 26) });
            g.FillPolygon(gold, new[] { new Point(14, 17), new Point(18, 17), new Point(16, 20) });
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}


