using System.Drawing;
using System.Windows.Forms;

namespace AIFileButler;

/// <summary>
/// The unified app window: a left sidebar navigates between pages (Dashboard,
/// Settings, People, Reminders, Memories, History) shown in the content area.
/// Each non-dashboard page reuses an existing Form, hosted as a child control
/// (TopLevel = false) so all the logic is shared with no duplication.
/// </summary>
public sealed class MainWindow : Form
{
    private readonly Config _cfg;
    private readonly Watcher _watcher;

    private readonly Panel _sidebar = new() { Dock = DockStyle.Left, Width = 212 };
    private readonly Panel _navHost = new() { Dock = DockStyle.Fill };
    private readonly Panel _brand = new() { Dock = DockStyle.Top, Height = 74 };
    private readonly Label _brandText = new();
    private readonly Panel _content = new() { Dock = DockStyle.Fill };
    private readonly Panel _header = new() { Dock = DockStyle.Top, Height = 58 };
    private readonly Label _pageTitle = new();
    private readonly Panel _pageHost = new() { Dock = DockStyle.Fill };

    private readonly Dictionary<string, Button> _nav = new();
    private readonly Dictionary<string, Panel> _navBar = new();
    private Control? _page;
    private string _current = "";

    private static readonly (string Key, string Emoji, string LabelKey)[] Pages =
    {
        ("dashboard",  "🏠", "nav_dashboard"),
        ("search",     "🔎", "nav_search"),
        ("duplicates", "🧹", "nav_duplicates"),
        ("settings",   "⚙",  "nav_settings"),
        ("people",     "🧑", "nav_people"),
        ("reminders",  "⏰", "nav_reminders"),
        ("memories",   "📅", "nav_memories"),
        ("history",    "🕘", "nav_history"),
    };

    /// <summary>Raised after the user saves Settings, so the tray menu / watcher
    /// state can be refreshed by the owner.</summary>
    public event Action? SettingsChanged;

    public MainWindow(Config cfg, Watcher watcher)
    {
        _cfg = cfg;
        _watcher = watcher;

        Text = "AI File Butler";
        Icon = AppArt.Load();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 560);
        Size = new Size(1120, 680);
        Font = new Font("Segoe UI", 9.5f);

        BuildSidebar();
        BuildContent();
        Controls.Add(_content);
        Controls.Add(_sidebar);

        HandleCreated += (_, _) => Theme.DarkTitleBar(Handle, Theme.IsDark);
        ApplyTheme();
        Navigate("dashboard");

        // Tray app: closing just hides the window, it keeps running in the tray.
        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        };

        // Ctrl+K jumps straight to search (the palette gesture Windows users expect).
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.K) { e.Handled = true; Navigate("search"); } };
    }

    /// <summary>Show (and focus) the window, optionally jumping to a page.</summary>
    public void Open(string? page = null)
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
        if (page is not null) Navigate(page);
        else if (_current == "dashboard") Navigate("dashboard"); // refresh dashboard
    }

    // ---- layout -----------------------------------------------------------

    private void BuildSidebar()
    {
        _brandText.Text = "AI File Butler";
        _brandText.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
        _brandText.ForeColor = Theme.Accent;
        _brandText.AutoSize = true;
        _brandText.Location = new Point(58, 26);

        var logo = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(34, 34), Location = new Point(16, 20) };
        try { logo.Image = AppArt.Load().ToBitmap(); } catch { }
        _brand.Controls.Add(_brandText);
        _brand.Controls.Add(logo);

        // build nav buttons, add to _navHost in reverse so the first ends up on top
        foreach (var (key, emoji, labelKey) in Pages)
        {
            var b = new Button
            {
                Dock = DockStyle.Top,
                Height = 46,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10.5f),
                Padding = new Padding(22, 0, 0, 0),
                Cursor = Cursors.Hand,
                Text = $"{emoji}    {L.S(labelKey)}",
                Tag = key,
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (_, _) => Navigate(key);

            var bar = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = Theme.Accent, Visible = false };
            b.Controls.Add(bar);

            _nav[key] = b;
            _navBar[key] = bar;
        }
        for (int i = Pages.Length - 1; i >= 0; i--) _navHost.Controls.Add(_nav[Pages[i].Key]);

        _sidebar.Controls.Add(_navHost);
        _sidebar.Controls.Add(_brand);
    }

    private void BuildContent()
    {
        _pageTitle.AutoSize = true;
        _pageTitle.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
        _pageTitle.Location = new Point(22, 14);
        _header.Controls.Add(_pageTitle);
        _header.Paint += (_, e) =>
        {
            using var p = new Pen(Theme.IsDark ? Color.FromArgb(52, 54, 64) : Color.FromArgb(233, 234, 239));
            e.Graphics.DrawLine(p, 0, _header.Height - 1, _header.Width, _header.Height - 1);
        };

        _content.Controls.Add(_pageHost);
        _content.Controls.Add(_header);
    }

    // ---- navigation -------------------------------------------------------

    private void Navigate(string key)
    {
        _current = key;
        _pageTitle.Text = L.S(Array.Find(Pages, p => p.Key == key).LabelKey);
        SetActive(key);

        _pageHost.SuspendLayout();
        var old = _page;
        _pageHost.Controls.Clear();
        old?.Dispose();

        _page = BuildPage(key);
        _page.Dock = DockStyle.Fill;
        _pageHost.Controls.Add(_page);
        _pageHost.ResumeLayout();
    }

    private Control BuildPage(string key) => key switch
    {
        "search"     => BuildSearch(),
        "duplicates" => BuildDuplicates(),
        "settings"   => Embed(MakeSettings()),
        "people"    => Embed(new PeopleForm()),
        "reminders" => Embed(new ExpiryForm()),
        "memories"  => Embed(new MemoriesForm(_cfg.DestRoot)),
        "history"   => Embed(new HistoryForm()),
        _           => BuildDashboard(),
    };

    // ---- semantic search page --------------------------------------------

    private Control BuildSearch()
    {
        bool dark = Theme.IsDark;
        var subFg = dark ? Color.FromArgb(150, 153, 165) : Color.FromArgb(110, 113, 122);

        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 16, 22, 16) };

        var query = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11f), PlaceholderText = L.S("search_placeholder") };
        var go = new Button
        {
            Text = L.S("search_btn"), Dock = DockStyle.Right, Width = 110, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold), BackColor = Theme.Accent, ForeColor = Color.White, Tag = "primary",
        };
        go.FlatAppearance.BorderColor = Theme.Accent;
        var gap = new Panel { Dock = DockStyle.Right, Width = 8 };
        var bar = new Panel { Dock = DockStyle.Top, Height = 40 };
        bar.Controls.Add(query); bar.Controls.Add(gap); bar.Controls.Add(go);

        var status = new Label { Dock = DockStyle.Top, Height = 30, ForeColor = subFg, Padding = new Padding(2, 8, 0, 0), Font = new Font("Segoe UI", 9.5f) };

        var results = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, Font = new Font("Segoe UI", 9.5f) };
        results.Columns.Add(L.S("search_col_file"), 230);
        results.Columns.Add(L.S("search_col_folder"), 130);
        results.Columns.Add(L.S("search_col_match"), 420);
        results.Columns.Add(L.S("search_col_score"), 70);
        results.DoubleClick += (_, _) =>
        {
            if (results.SelectedItems.Count > 0 && results.SelectedItems[0].Tag is string p && File.Exists(p))
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = p, UseShellExecute = true }); } catch { }
        };

        var rebuild = new Button
        {
            Text = L.S("search_rebuild"), Dock = DockStyle.Right, Width = 150, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
        };
        rebuild.FlatAppearance.BorderColor = Color.LightGray;
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(0, 6, 0, 0) };
        bottom.Controls.Add(rebuild);

        void Search()
        {
            var q = query.Text.Trim();
            if (q.Length == 0) return;
            status.Text = L.S("search_searching");
            Task.Run(() => Embedder.Embed(q, _cfg.OllamaUrl, _cfg.EmbedModel)).ContinueWith(t =>
            {
                if (IsDisposed) return;
                BeginInvoke(() =>
                {
                    results.Items.Clear();
                    if (t.Result is not float[] qv) { status.Text = L.S("search_no_model"); return; }
                    var hits = SearchIndex.Query(qv, 30);
                    foreach (var (e, score) in hits)
                        results.Items.Add(new ListViewItem(new[] { Path.GetFileName(e.Path), e.Folder, e.Snippet.Replace('\n', ' ').Replace('\r', ' '), score.ToString("F2") }) { Tag = e.Path });
                    status.Text = hits.Count == 0 ? L.S("search_none") : string.Format(L.S("search_results"), hits.Count);
                });
            });
        }
        go.Click += (_, _) => Search();
        query.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Search(); } };
        rebuild.Click += (_, _) =>
        {
            rebuild.Enabled = false; rebuild.Text = "…"; status.Text = L.S("search_indexing");
            Task.Run(() => SearchIndex.Rebuild(_cfg.DestRoot, _cfg.OllamaUrl, _cfg.EmbedModel)).ContinueWith(t =>
            {
                if (IsDisposed) return;
                BeginInvoke(() =>
                {
                    rebuild.Enabled = true; rebuild.Text = L.S("search_rebuild");
                    status.Text = string.Format(L.S("search_indexed"), t.Result);
                });
            });
        };

        status.Text = SearchIndex.Count == 0 ? L.S("search_empty") : string.Format(L.S("search_ready"), SearchIndex.Count);

        root.Controls.Add(results);
        root.Controls.Add(bottom);
        root.Controls.Add(status);
        root.Controls.Add(bar);
        query.Select();
        return root;
    }

    // ---- duplicate finder page -------------------------------------------

    private static string HumanSize(long b)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double s = b; int i = 0;
        while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
        return $"{s:0.#} {u[i]}";
    }

    private Control BuildDuplicates()
    {
        bool dark = Theme.IsDark;
        var subFg = dark ? Color.FromArgb(150, 153, 165) : Color.FromArgb(110, 113, 122);
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 16, 22, 16) };

        string scanRoot = _cfg.DestRoot;
        var groups = new List<DuplicateFinder.DupGroup>();

        var results = new ListView { Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true, ShowGroups = true, Font = new Font("Segoe UI", 9.5f) };
        results.Columns.Add(L.S("dup_col_file"), 210);
        results.Columns.Add(L.S("dup_col_folder"), 240);
        results.Columns.Add(L.S("dup_col_size"), 80);
        results.DoubleClick += (_, _) =>
        {
            if (results.SelectedItems.Count > 0 && results.SelectedItems[0].Tag is string p && File.Exists(p))
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = p, UseShellExecute = true }); } catch { }
        };

        var folderLbl = new Label { Dock = DockStyle.Fill, Text = scanRoot, ForeColor = subFg, Padding = new Padding(0, 8, 8, 0), AutoEllipsis = true };
        var scan = new Button { Text = L.S("dup_scan"), Dock = DockStyle.Right, Width = 120, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10f, FontStyle.Bold), BackColor = Theme.Accent, ForeColor = Color.White, Tag = "primary" };
        scan.FlatAppearance.BorderColor = Theme.Accent;
        var choose = new Button { Text = L.S("dup_choose"), Dock = DockStyle.Right, Width = 120, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        choose.FlatAppearance.BorderColor = Color.LightGray;
        var mode = new ComboBox { Dock = DockStyle.Right, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        mode.Items.Add(L.S("dup_mode_exact")); mode.Items.Add(L.S("dup_mode_similar")); mode.Items.Add(L.S("dup_mode_large"));
        mode.SelectedIndex = 0;
        var bar = new Panel { Dock = DockStyle.Top, Height = 36 };
        bar.Controls.Add(folderLbl);
        bar.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
        bar.Controls.Add(mode);
        bar.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
        bar.Controls.Add(choose);
        bar.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
        bar.Controls.Add(scan);

        var status = new Label { Dock = DockStyle.Top, Height = 28, ForeColor = subFg, Padding = new Padding(2, 6, 0, 0), Font = new Font("Segoe UI", 9.5f), Text = L.S("dup_hint") };

        var del = new Button { Text = L.S("dup_delete"), Dock = DockStyle.Right, Width = 250, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        del.FlatAppearance.BorderColor = Color.LightGray;
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(0, 6, 0, 0) };
        bottom.Controls.Add(del);

        void Populate()
        {
            results.BeginUpdate();
            results.Items.Clear(); results.Groups.Clear();
            long wasted = 0;
            foreach (var g in groups)
            {
                var header = g.Exact
                    ? string.Format(L.S("dup_group"), g.Files.Count, HumanSize(g.Size))
                    : string.Format(L.S("dup_group_sim"), g.Files.Count);
                var lvg = new ListViewGroup(header);
                results.Groups.Add(lvg);
                for (int i = 0; i < g.Files.Count; i++)
                {
                    var fr = g.Files[i];
                    // exact: keep the first, tick the rest; similar: review-only, nothing pre-ticked
                    results.Items.Add(new ListViewItem(new[] { Path.GetFileName(fr.Path), TrimDir(Path.GetDirectoryName(fr.Path) ?? ""), HumanSize(fr.Size) })
                    { Group = lvg, Tag = fr.Path, Checked = g.Exact && i > 0 });
                }
                wasted += g.Wasted;
            }
            results.EndUpdate();
            if (groups.Count == 0) status.Text = L.S("dup_none");
            else status.Text = groups[0].Exact
                ? string.Format(L.S("dup_found"), groups.Count, HumanSize(wasted))
                : string.Format(L.S("dup_found_sim"), groups.Count);
        }

        void PopulateLargest(DiskUsage.Result du)
        {
            results.BeginUpdate();
            results.Items.Clear(); results.Groups.Clear();
            foreach (var bf in du.Largest)
                results.Items.Add(new ListViewItem(new[] { Path.GetFileName(bf.Path), TrimDir(Path.GetDirectoryName(bf.Path) ?? ""), HumanSize(bf.Size) }) { Tag = bf.Path });
            results.EndUpdate();
            status.Text = du.FileCount == 0 ? L.S("dup_none")
                : string.Format(L.S("dup_large_result"), du.FileCount, HumanSize(du.TotalBytes), Path.GetFileName(du.BiggestFolder), HumanSize(du.BiggestFolderBytes));
        }

        void DoScan()
        {
            int m = mode.SelectedIndex;
            scan.Enabled = false; scan.Text = "…"; status.Text = L.S("dup_scanning");
            var r = scanRoot;
            if (m == 2)
            {
                Task.Run(() => DiskUsage.Scan(r)).ContinueWith(t =>
                {
                    if (IsDisposed) return;
                    BeginInvoke(() => { scan.Enabled = true; scan.Text = L.S("dup_scan"); groups = new(); PopulateLargest(t.Result); });
                });
            }
            else
            {
                Task.Run(() => m == 1 ? DuplicateFinder.FindSimilarImages(r, 10) : DuplicateFinder.FindExact(r)).ContinueWith(t =>
                {
                    if (IsDisposed) return;
                    BeginInvoke(() => { scan.Enabled = true; scan.Text = L.S("dup_scan"); groups = t.Result ?? new(); Populate(); });
                });
            }
        }
        scan.Click += (_, _) => DoScan();
        choose.Click += (_, _) =>
        {
            using var fb = new FolderBrowserDialog { SelectedPath = scanRoot };
            if (fb.ShowDialog(this) == DialogResult.OK) { scanRoot = fb.SelectedPath; folderLbl.Text = scanRoot; }
        };
        del.Click += (_, _) =>
        {
            var ticked = results.CheckedItems.Cast<ListViewItem>().ToList();
            if (ticked.Count == 0) return;
            int deleted = 0;
            foreach (var grp in ticked.GroupBy(it => it.Group))
            {
                var toDelete = grp.ToList();
                if (grp.Key is ListViewGroup lvg) // grouped duplicates: never delete the last copy of a set
                {
                    if (toDelete.Count >= lvg.Items.Count) toDelete = toDelete.Skip(1).ToList();
                }
                foreach (var it in toDelete)
                {
                    if (it.Tag is not string path) continue;
                    try
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        it.Remove(); deleted++;
                    }
                    catch { }
                }
            }
            status.Text = string.Format(L.S("dup_deleted"), deleted);
        };

        // preview panel — shows the selected image so you can eyeball photo matches
        var previewBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = dark ? Color.FromArgb(42, 44, 54) : Color.FromArgb(245, 246, 248) };
        var previewInfo = new Label { Dock = DockStyle.Bottom, Height = 42, ForeColor = subFg, Padding = new Padding(6, 6, 6, 0), Font = new Font("Segoe UI", 9f) };
        var preview = new Panel { Dock = DockStyle.Right, Width = 300, Padding = new Padding(12, 0, 0, 0) };
        preview.Controls.Add(previewBox); preview.Controls.Add(previewInfo);
        results.SelectedIndexChanged += (_, _) =>
        {
            previewBox.Image?.Dispose(); previewBox.Image = null; previewInfo.Text = "";
            if (results.SelectedItems.Count == 0 || results.SelectedItems[0].Tag is not string p) return;
            previewInfo.Text = Path.GetFileName(p);
            if (!IsImageFile(p) || !File.Exists(p)) return;
            try
            {
                using var tmp = Image.FromStream(new MemoryStream(File.ReadAllBytes(p))); // copy so the file isn't locked
                previewBox.Image = new Bitmap(tmp);
                previewInfo.Text = $"{Path.GetFileName(p)}  ·  {tmp.Width}×{tmp.Height}";
            }
            catch { }
        };

        var center = new Panel { Dock = DockStyle.Fill };
        center.Controls.Add(results); // Fill (added first so it yields to the docked preview)
        center.Controls.Add(preview);

        root.Controls.Add(center);
        root.Controls.Add(bottom);
        root.Controls.Add(status);
        root.Controls.Add(bar);
        return root;
    }

    private static readonly HashSet<string> ImgExts = new(StringComparer.OrdinalIgnoreCase)
    { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp" };
    private static bool IsImageFile(string p) => ImgExts.Contains(Path.GetExtension(p));

    private static string TrimDir(string p) => p.Length <= 52 ? p : "…" + p[^51..];

    private SettingsForm MakeSettings()
    {
        var s = new SettingsForm(_cfg, _watcher, embedded: true);
        s.Applied += () =>
        {
            // language / theme may have changed — refresh the shell and tray
            foreach (var (k, _, lk) in Pages)
                _nav[k].Text = _nav[k].Text[.._nav[k].Text.IndexOf(' ')] + "    " + L.S(lk);
            ApplyTheme();
            _pageTitle.Text = L.S(Array.Find(Pages, p => p.Key == _current).LabelKey);
            SettingsChanged?.Invoke();
        };
        return s;
    }

    private static Control Embed(Form f)
    {
        f.TopLevel = false;
        f.FormBorderStyle = FormBorderStyle.None;
        f.Dock = DockStyle.Fill;
        f.Visible = true;
        return f;
    }

    private void SetActive(string key)
    {
        bool dark = Theme.IsDark;
        var sideFg = dark ? Color.FromArgb(200, 202, 212) : Color.FromArgb(64, 67, 77);
        var selBg = dark ? Color.FromArgb(38, 41, 52) : Color.FromArgb(232, 240, 253);
        var sideBg = dark ? Color.FromArgb(22, 23, 28) : Color.FromArgb(247, 248, 250);
        foreach (var (k, b) in _nav)
        {
            bool on = k == key;
            b.BackColor = on ? selBg : sideBg;
            b.ForeColor = on ? Theme.Accent : sideFg;
            b.Font = new Font("Segoe UI", 10.5f, on ? FontStyle.Bold : FontStyle.Regular);
            b.FlatAppearance.MouseOverBackColor = on ? selBg : (dark ? Color.FromArgb(32, 34, 42) : Color.FromArgb(238, 240, 244));
            _navBar[k].Visible = on;
        }
    }

    private void ApplyTheme()
    {
        bool dark = Theme.IsDark;
        var sideBg = dark ? Color.FromArgb(22, 23, 28) : Color.FromArgb(247, 248, 250);
        var contentBg = dark ? Color.FromArgb(32, 33, 40) : Color.White;
        var title = dark ? Color.White : Color.FromArgb(30, 32, 38);

        BackColor = contentBg;
        _sidebar.BackColor = sideBg;
        _navHost.BackColor = sideBg;
        _brand.BackColor = sideBg;
        _content.BackColor = contentBg;
        _header.BackColor = contentBg;
        _pageHost.BackColor = contentBg;
        _pageTitle.ForeColor = title;
        _brandText.ForeColor = Theme.Accent;

        int v = dark ? 1 : 0;
        try { Theme.DarkTitleBar(Handle, dark); } catch { }

        SetActive(_current.Length == 0 ? "dashboard" : _current);
        _header.Invalidate();
    }

    // ---- dashboard --------------------------------------------------------

    private Control BuildDashboard()
    {
        bool dark = Theme.IsDark;
        var cardBg = dark ? Color.FromArgb(42, 44, 54) : Color.FromArgb(248, 249, 251);
        var subFg = dark ? Color.FromArgb(150, 153, 165) : Color.FromArgb(110, 113, 122);
        var fg = dark ? Color.FromArgb(232, 233, 240) : Color.FromArgb(34, 36, 42);

        var root = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(22, 18, 22, 22) };

        // a single-column table makes every full-width card track the window width
        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, GrowStyle = TableLayoutPanelGrowStyle.AddRows,
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        int r = 0;
        void AddRow(Control c, DockStyle dock, int top) { c.Margin = new Padding(0, top, 0, 0); c.Dock = dock; stack.Controls.Add(c, 0, r++); }
        Label Section(string text, int top) => new()
        { Text = text, AutoSize = true, ForeColor = subFg, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Margin = new Padding(2, top, 0, 0) };

        // ---- status hero ----
        bool ai = _watcher.Backend == "ollama";
        string mode = _watcher.Paused ? L.S("m_pause")
            : (_watcher.Auto ? L.S("dash_mode_auto") : L.S("dash_mode_manual"));
        var hero = new Panel { BackColor = cardBg, Height = 92 };
        hero.Controls.Add(new Label
        {
            Text = (ai ? "🟢  " : "⚪  ") + (ai ? L.S("dash_ai_on") : L.S("dash_ai_off")),
            ForeColor = ai ? Theme.Accent : fg, AutoSize = true,
            Font = new Font("Segoe UI", 12.5f, FontStyle.Bold), Location = new Point(18, 17),
        });
        hero.Controls.Add(new Label
        {
            Text = $"{mode}  ·  " + string.Format(L.S("dash_session"), _watcher.SessionCount),
            ForeColor = subFg, AutoSize = true, Font = new Font("Segoe UI", 10f), Location = new Point(20, 53),
        });
        AddRow(hero, DockStyle.Fill, 0);

        // ---- quick actions ----
        AddRow(Section(L.S("dash_quick"), 16), DockStyle.Left, 16);
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        actions.Controls.Add(ActionButton("✨  " + L.S("m_organize"), true, OrganizePreview));
        actions.Controls.Add(ActionButton("📂  " + L.S("m_open"), false, OpenSorted));
        actions.Controls.Add(ActionButton((_watcher.Paused ? "▶  " + L.S("dash_resume") : "⏸  " + L.S("m_pause")), false,
            () => { _watcher.Paused = !_watcher.Paused; SettingsChanged?.Invoke(); RefreshDashboard(); }));
        AddRow(actions, DockStyle.Left, 6);

        // ---- library summary ----
        var (photos, docs, people) = Memories.Summary(_cfg.DestRoot);
        AddRow(Section(L.S("dash_library"), 18), DockStyle.Left, 18);
        var lib = new FlowLayoutPanel { AutoSize = true };
        lib.Controls.Add(StatCard(cardBg, subFg, photos.ToString(), "📷 " + L.S("dash_photos")));
        lib.Controls.Add(StatCard(cardBg, subFg, docs.ToString(), "📄 " + L.S("dash_docs")));
        lib.Controls.Add(StatCard(cardBg, subFg, people.ToString(), "🧑 " + L.S("nav_people")));
        AddRow(lib, DockStyle.Left, 6);

        // ---- upcoming reminders ----
        AddRow(Section("⏰  " + L.S("dash_upcoming"), 18), DockStyle.Left, 18);
        var up = Reminders.All().Where(i => Reminders.DaysLeft(i) <= 90)
            .OrderBy(Reminders.DaysLeft).Take(5).ToList();
        int rows = Math.Max(up.Count, 1);
        var upCard = new Panel { BackColor = cardBg, Height = 24 + rows * 27, Padding = new Padding(16, 12, 16, 12) };
        var upFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = false };
        if (up.Count == 0)
            upFlow.Controls.Add(new Label { Text = "✓  " + L.S("dash_none_up"), AutoSize = true, ForeColor = subFg, Font = new Font("Segoe UI", 10f), Margin = new Padding(0, 3, 0, 3) });
        else
            foreach (var i in up)
            {
                int d = Reminders.DaysLeft(i);
                string when = d < 0 ? L.S("dash_expired") : string.Format(L.S("dash_in_days"), d);
                var c = d < 14 ? Color.OrangeRed : d < 30 ? Color.DarkOrange : fg;
                upFlow.Controls.Add(new Label
                {
                    Text = $"{i.Kind}  ·  {when}    ({Path.GetFileName(i.File)})",
                    AutoSize = true, ForeColor = c, Font = new Font("Segoe UI", 10f), Margin = new Padding(0, 3, 0, 3),
                });
            }
        upCard.Controls.Add(upFlow);
        AddRow(upCard, DockStyle.Fill, 6);

        root.Controls.Add(stack);
        return root;
    }

    private void RefreshDashboard()
    {
        if (_current == "dashboard") Navigate("dashboard");
    }

    // Plan off-thread (the AI is slow), then let the user review the moves before
    // anything is touched, then apply only what they kept.
    private void OrganizePreview()
    {
        Task.Run(() => { try { return _watcher.PreviewMoves(); } catch { return new List<Watcher.Pending>(); } })
            .ContinueWith(t =>
            {
                if (IsDisposed) return;
                BeginInvoke(() =>
                {
                    var moves = t.Result;
                    if (moves.Count == 0) { MessageBox.Show(this, L.S("n_nothing"), "AI File Butler"); return; }
                    using var dlg = new PreviewForm(moves);
                    if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Accepted.Count > 0)
                        Task.Run(() => _watcher.ApplyMoves(dlg.Accepted))
                            .ContinueWith(_ => { if (!IsDisposed) BeginInvoke(RefreshDashboard); });
                });
            });
    }

    private Control StatCard(Color bg, Color sub, string number, string label)
    {
        var p = new Panel { BackColor = bg, Size = new Size(158, 80), Margin = new Padding(0, 0, 12, 0) };
        p.Controls.Add(new Label { Text = number, ForeColor = Theme.Accent, AutoSize = true, Font = new Font("Segoe UI", 22f, FontStyle.Bold), Location = new Point(14, 9) });
        p.Controls.Add(new Label { Text = label, ForeColor = sub, AutoSize = true, Font = new Font("Segoe UI", 9.5f), Location = new Point(16, 52) });
        return p;
    }

    private Button ActionButton(string text, bool primary, Action onClick)
    {
        var b = new Button
        {
            Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Padding = new Padding(14, 8, 14, 8), Margin = new Padding(0, 0, 10, 0),
            Font = new Font("Segoe UI", 9.75f, primary ? FontStyle.Bold : FontStyle.Regular),
        };
        if (primary) { b.BackColor = Theme.Accent; b.ForeColor = Color.White; b.FlatAppearance.BorderColor = Theme.Accent; }
        else
        {
            bool dark = Theme.IsDark;
            b.BackColor = dark ? Color.FromArgb(50, 52, 63) : Color.White;
            b.ForeColor = dark ? Color.FromArgb(232, 233, 240) : Color.FromArgb(40, 42, 48);
            b.FlatAppearance.BorderColor = dark ? Color.FromArgb(70, 72, 84) : Color.FromArgb(214, 216, 222);
        }
        b.Click += (_, _) => onClick();
        return b;
    }

    private void OpenSorted()
    {
        try
        {
            var dir = _cfg.DestRoot;
            if (Directory.Exists(dir))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch { }
    }
}
