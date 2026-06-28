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
                    Theme.IsDark = scfg.DarkMode;
                    Form sf = args.Length > 3 && args[3] == "welcome" ? new WelcomeForm()
                        : args.Length > 3 && args[3] == "history" ? new HistoryForm()
                        : new SettingsForm(scfg, new Watcher(scfg));
                    sf.Show();
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
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _organizeItem;
    private readonly ToolStripMenuItem _undoItem;
    private readonly ToolStripMenuItem _openItem;
    private readonly ToolStripMenuItem _historyItem;
    private readonly ToolStripMenuItem _helpItem;
    private readonly ToolStripMenuItem _quitItem;
    private SettingsForm? _settings;

    public ButlerContext()
    {
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();
        _cfg = Config.Load();
        L.Lang = string.IsNullOrEmpty(_cfg.Language) ? "en" : _cfg.Language;
        Theme.IsDark = _cfg.DarkMode;
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
        _settingsItem = new ToolStripMenuItem("", null, (_, _) => OpenSettings())
            { Font = new Font(System.Drawing.SystemFonts.MenuFont!, System.Drawing.FontStyle.Bold) };
        _organizeItem = new ToolStripMenuItem("", null, (_, _) => OrganizeNow());
        _undoItem = new ToolStripMenuItem("", null, (_, _) => _watcher.UndoLast());
        _openItem = new ToolStripMenuItem("", null, (_, _) => OpenSorted());
        _historyItem = new ToolStripMenuItem("", null, (_, _) => new HistoryForm().Show());
        _helpItem = new ToolStripMenuItem("", null, (_, _) => new HelpForm().ShowDialog());
        _quitItem = new ToolStripMenuItem("", null, (_, _) => Quit());

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _statusItem,
            _watchItem,
            new ToolStripSeparator(),
            _settingsItem,
            new ToolStripSeparator(),
            _autoItem,
            _pauseItem,
            new ToolStripSeparator(),
            _organizeItem,
            _undoItem,
            _openItem,
            _historyItem,
            _helpItem,
            new ToolStripSeparator(),
            _startupItem,
            _quitItem,
        });
        menu.Opening += (_, _) => RefreshMenu();

        _tray = new NotifyIcon
        {
            Icon = MakeIcon(),
            Text = "AI File Butler",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => OpenSettings();

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
                OpenSettings();
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

        // labels follow the language chosen in Settings
        _settingsItem.Text = L.S("m_settings");
        _autoItem.Text = L.S("m_auto");
        _pauseItem.Text = L.S("m_pause");
        _organizeItem.Text = L.S("m_organize");
        _undoItem.Text = L.S("m_undo");
        _openItem.Text = L.S("m_open");
        _historyItem.Text = L.S("m_history");
        _helpItem.Text = L.S("m_help");
        _startupItem.Text = L.S("startup");
        _quitItem.Text = L.S("m_quit");

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

    private void OpenSettings()
    {
        if (_settings is { IsDisposed: false })
        {
            if (_settings.WindowState == FormWindowState.Minimized)
                _settings.WindowState = FormWindowState.Normal;
            _settings.Activate();
            _settings.BringToFront();
            return;
        }
        _settings = new SettingsForm(_cfg, _watcher);
        _settings.FormClosed += (_, _) => { _settings = null; RefreshMenu(); };
        _settings.Show();
        _settings.Activate();
    }

    private void OrganizeNow()
    {
        var plans = _watcher.ScanNow(apply: true);
        if (plans.Count == 0)
            _tray.ShowBalloonTip(3000, "AI File Butler", L.S("n_nothing"), ToolTipIcon.Info);
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
