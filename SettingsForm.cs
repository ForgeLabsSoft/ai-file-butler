using System.Drawing;
using System.Windows.Forms;

namespace AIFileButler;

/// <summary>
/// Resizable/maximizable settings window. Save/Cancel stay pinned at the bottom;
/// everything else scrolls, so nothing is ever clipped. Fully localized.
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly Config _cfg;
    private readonly Watcher _watcher;

    private readonly ListBox _folders = new();
    private readonly TextBox _dest = new();
    private readonly ComboBox _model = new();
    private readonly ComboBox _lang = new();
    private readonly Label _aiStatus = new();
    private readonly CheckBox _auto = new();
    private readonly CheckBox _startup = new();
    private readonly NumericUpDown _poll = new();
    private readonly NumericUpDown _minAge = new();
    private readonly NumericUpDown _review = new();
    private readonly ComboBox _musicBy = new();
    private readonly ComboBox _movieBy = new();
    private readonly ComboBox _photoBy = new();
    private readonly CheckBox _sepParties = new();

    private static readonly string[] MusicOpts = { "artist", "genre", "year", "alpha", "none" };
    private static readonly string[] MovieOpts = { "genre", "actor", "year", "alpha", "none" };
    private static readonly string[] PhotoOpts = { "date", "location", "person", "alpha", "none" };

    // controls whose .Text follows the language
    private readonly List<(Control C, string Key)> _loc = new();

    private static readonly Color Accent = Color.FromArgb(43, 139, 234);

    public SettingsForm(Config cfg, Watcher watcher)
    {
        _cfg = cfg;
        _watcher = watcher;
        L.Lang = string.IsNullOrEmpty(cfg.Language) ? "en" : cfg.Language;

        Text = "AI File Butler — Settings";
        Icon = AppArt.Load();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 600);
        Size = new Size(700, 720);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9.5f);

        BuildLayout();
        LoadValues();
        Localize();
    }

    private Control Reg(Control c, string key) { _loc.Add((c, key)); return c; }

    private void BuildLayout()
    {
        // --- bottom action bar (always visible) ---
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(16, 10, 16, 10) };
        var actions = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        actions.Controls.Add(Reg(MakeButton("", (_, _) => SaveAndClose(), true), "save"));
        actions.Controls.Add(Reg(MakeButton("", (_, _) => Close(), false), "cancel"));
        actions.Controls.Add(Reg(MakeButton("", (_, _) => new HelpForm().ShowDialog(this), false), "help"));
        bar.Controls.Add(actions);
        Controls.Add(bar);

        // --- scrollable content ---
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16, 16, 16, 0) };
        Controls.Add(scroll);
        Controls.SetChildIndex(scroll, 0);

        var root = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Width = 1 };
        root.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        scroll.Controls.Add(root);
        scroll.Resize += (_, _) => root.Width = scroll.ClientSize.Width - 4;

        // header: brand + subtitle + language picker
        var head = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) };
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var brand = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
        brand.Controls.Add(new Label { Text = "AI File Butler", AutoSize = true, ForeColor = Accent, Font = new Font("Segoe UI", 16f, FontStyle.Bold) });
        brand.Controls.Add(Reg(new Label { AutoSize = true, ForeColor = Color.Gray }, "subtitle"));
        head.Controls.Add(brand, 0, 0);
        var langWrap = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Anchor = AnchorStyles.Right };
        langWrap.Controls.Add(Reg(new Label { AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(0, 4, 0, 2) }, "language"));
        _lang.DropDownStyle = ComboBoxStyle.DropDownList;
        _lang.Width = 130;
        foreach (var (_, name) in L.Languages) _lang.Items.Add(name);
        _lang.SelectedIndexChanged += (_, _) => OnLanguageChanged();
        langWrap.Controls.Add(_lang);
        head.Controls.Add(langWrap, 1, 0);
        root.Controls.Add(head);

        // watched folders (fixed height so it never eats the window)
        var gFolders = Group("folders");
        var fLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Height = 150 };
        fLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _folders.Dock = DockStyle.Fill; _folders.IntegralHeight = false;
        var fBtns = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
        fBtns.Controls.Add(Reg(MakeButton("", (_, _) => AddFolder(), false), "add"));
        fBtns.Controls.Add(Reg(MakeButton("", (_, _) => { if (_folders.SelectedIndex >= 0) _folders.Items.RemoveAt(_folders.SelectedIndex); }, false), "remove"));
        fLayout.Controls.Add(_folders, 0, 0);
        fLayout.Controls.Add(fBtns, 1, 0);
        gFolders.Controls.Add(fLayout);
        gFolders.Height = 180;
        root.Controls.Add(gFolders);

        // destination
        var gDest = Group("dest");
        var dLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true };
        dLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        dLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _dest.Dock = DockStyle.Fill; _dest.Margin = new Padding(0, 6, 0, 6);
        dLayout.Controls.Add(_dest, 0, 0);
        dLayout.Controls.Add(Reg(MakeButton("", (_, _) => BrowseDest(), false), "browse"), 1, 0);
        gDest.Controls.Add(dLayout);
        gDest.Height = 82;
        root.Controls.Add(gDest);

        // AI model
        var gModel = Group("model");
        var mLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, AutoSize = true };
        mLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _model.Dock = DockStyle.Fill; _model.DropDownStyle = ComboBoxStyle.DropDown;
        _model.Items.AddRange(new object[] { "llama3.1:8b", "llama3.2:3b", "qwen2.5:7b", "mistral:7b" });
        mLayout.Controls.Add(_model, 0, 0);
        mLayout.Controls.Add(Reg(MakeButton("", (_, _) => TestAi(), false), "test"), 1, 0);
        _aiStatus.AutoSize = true; _aiStatus.Margin = new Padding(2, 8, 0, 0);
        mLayout.Controls.Add(_aiStatus, 0, 1); mLayout.SetColumnSpan(_aiStatus, 2);
        var hint = Reg(new Label { AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(2, 4, 0, 0) }, "model_hint");
        mLayout.Controls.Add(hint, 0, 2); mLayout.SetColumnSpan(hint, 2);
        gModel.Controls.Add(mLayout);
        gModel.Height = 110;
        root.Controls.Add(gModel);

        // behavior
        var gBehavior = Group("behavior");
        ConfigNumeric(_poll, 2, 120); ConfigNumeric(_minAge, 0, 600); ConfigNumeric(_review, 0, 95);
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, RowCount = 1, AutoSize = true };
        grid.Controls.Add(Reg(new Label { AutoSize = true, Margin = new Padding(0, 6, 6, 0) }, "poll"), 0, 0);
        grid.Controls.Add(_poll, 1, 0);
        grid.Controls.Add(Reg(new Label { AutoSize = true, Margin = new Padding(18, 6, 6, 0) }, "minage"), 2, 0);
        grid.Controls.Add(_minAge, 3, 0);
        var rev = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 1, AutoSize = true };
        rev.Controls.Add(Reg(new Label { AutoSize = true, Margin = new Padding(0, 6, 6, 0) }, "review_below"), 0, 0);
        rev.Controls.Add(_review, 1, 0);
        var bWrap = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, AutoSize = true };
        _auto.AutoSize = true; _startup.AutoSize = true;
        bWrap.Controls.Add(Reg(_auto, "auto"), 0, 0);
        bWrap.Controls.Add(Reg(_startup, "startup"), 0, 1);
        bWrap.Controls.Add(grid, 0, 2);
        bWrap.Controls.Add(rev, 0, 3);
        gBehavior.Controls.Add(bWrap);
        gBehavior.Height = 160;
        root.Controls.Add(gBehavior);

        // sorting schemes (music / movies / invoices)
        var gSort = Group("sorting");
        _musicBy.DropDownStyle = ComboBoxStyle.DropDownList; _musicBy.Width = 170;
        _movieBy.DropDownStyle = ComboBoxStyle.DropDownList; _movieBy.Width = 170;
        _photoBy.DropDownStyle = ComboBoxStyle.DropDownList; _photoBy.Width = 170;
        _sepParties.AutoSize = true;
        var sGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, AutoSize = true };
        sGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sGrid.Controls.Add(Reg(new Label { AutoSize = true, Margin = new Padding(0, 6, 12, 0) }, "music_by"), 0, 0);
        sGrid.Controls.Add(_musicBy, 1, 0);
        sGrid.Controls.Add(Reg(new Label { AutoSize = true, Margin = new Padding(0, 6, 12, 0) }, "movie_by"), 0, 1);
        sGrid.Controls.Add(_movieBy, 1, 1);
        sGrid.Controls.Add(Reg(new Label { AutoSize = true, Margin = new Padding(0, 6, 12, 0) }, "photo_by"), 0, 2);
        sGrid.Controls.Add(_photoBy, 1, 2);
        sGrid.Controls.Add(Reg(_sepParties, "sep_parties"), 0, 3);
        sGrid.SetColumnSpan(_sepParties, 2);
        gSort.Controls.Add(sGrid);
        gSort.Height = 160;
        root.Controls.Add(gSort);
    }

    private GroupBox Group(string key)
    {
        var g = new GroupBox { Dock = DockStyle.Top, Padding = new Padding(10, 14, 10, 10), Margin = new Padding(0, 4, 0, 10), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        Reg(g, key);
        return g;
    }

    private static void ConfigNumeric(NumericUpDown n, int min, int max)
    { n.Minimum = min; n.Maximum = max; n.Width = 70; n.Font = new Font("Segoe UI", 9.5f); }

    private Button MakeButton(string text, EventHandler onClick, bool primary)
    {
        var b = new Button
        {
            Text = text, AutoSize = true, Padding = new Padding(10, 4, 10, 4),
            FlatStyle = FlatStyle.Flat, Margin = new Padding(4), Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9.5f, primary ? FontStyle.Bold : FontStyle.Regular),
        };
        b.FlatAppearance.BorderColor = primary ? Accent : Color.LightGray;
        b.BackColor = primary ? Accent : Color.White;
        b.ForeColor = primary ? Color.White : Color.Black;
        b.Click += onClick;
        return b;
    }

    private void LoadValues()
    {
        _folders.Items.Clear();
        foreach (var d in _cfg.WatchDirs) _folders.Items.Add(d);
        _dest.Text = _cfg.DestRoot;
        _model.Text = _cfg.OllamaModel;
        _auto.Checked = _watcher.Auto;
        _startup.Checked = Startup.IsEnabled;
        _poll.Value = Math.Clamp(_cfg.PollIntervalSeconds, (int)_poll.Minimum, (int)_poll.Maximum);
        _minAge.Value = Math.Clamp(_cfg.MinAgeSeconds, (int)_minAge.Minimum, (int)_minAge.Maximum);
        _review.Value = Math.Clamp(_cfg.ReviewThreshold, (int)_review.Minimum, (int)_review.Maximum);
        _musicBy.Tag = _cfg.MusicBy; _movieBy.Tag = _cfg.MovieBy; _photoBy.Tag = _cfg.ImageBy;
        _sepParties.Checked = _cfg.SeparateInvoiceParties;
        var idx = Array.FindIndex(L.Languages, l => l.Code == L.Lang);
        _lang.SelectedIndex = idx < 0 ? 0 : idx;
    }

    private void Localize()
    {
        foreach (var (c, key) in _loc) c.Text = L.S(key);
        PopulateScheme(_musicBy, MusicOpts);
        PopulateScheme(_movieBy, MovieOpts);
        PopulateScheme(_photoBy, PhotoOpts);
    }

    private static void PopulateScheme(ComboBox combo, string[] opts)
    {
        string code = combo.Tag as string
                      ?? (combo.SelectedIndex >= 0 ? opts[combo.SelectedIndex] : opts[0]);
        combo.Items.Clear();
        foreach (var o in opts) combo.Items.Add(L.S("opt_" + o));
        int i = Array.IndexOf(opts, code);
        combo.SelectedIndex = i < 0 ? 0 : i;
        combo.Tag = null;
    }

    private void OnLanguageChanged()
    {
        if (_lang.SelectedIndex >= 0) L.Lang = L.Languages[_lang.SelectedIndex].Code;
        Localize();
    }

    private void AddFolder()
    {
        using var dlg = new FolderBrowserDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
            if (!_folders.Items.Contains(dlg.SelectedPath)) _folders.Items.Add(dlg.SelectedPath);
    }

    private void BrowseDest()
    {
        using var dlg = new FolderBrowserDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
            _dest.Text = dlg.SelectedPath;
    }

    private void TestAi()
    {
        _aiStatus.ForeColor = Color.Gray;
        _aiStatus.Text = L.S("testing");
        var url = _cfg.OllamaUrl;
        Task.Run(() => OllamaClassifier.IsAvailable(url)).ContinueWith(t =>
        {
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                _aiStatus.ForeColor = t.Result ? Color.SeaGreen : Color.OrangeRed;
                _aiStatus.Text = L.S(t.Result ? "ai_ok" : "ai_no");
            });
        });
    }

    private void SaveAndClose()
    {
        _cfg.WatchDirs = _folders.Items.Cast<object>().Select(o => o.ToString()!).ToList();
        _cfg.DestRoot = _dest.Text.Trim();
        _cfg.OllamaModel = _model.Text.Trim();
        _cfg.PollIntervalSeconds = (int)_poll.Value;
        _cfg.MinAgeSeconds = (int)_minAge.Value;
        _cfg.ReviewThreshold = (int)_review.Value;
        if (_musicBy.SelectedIndex >= 0) _cfg.MusicBy = MusicOpts[_musicBy.SelectedIndex];
        if (_movieBy.SelectedIndex >= 0) _cfg.MovieBy = MovieOpts[_movieBy.SelectedIndex];
        if (_photoBy.SelectedIndex >= 0) _cfg.ImageBy = PhotoOpts[_photoBy.SelectedIndex];
        _cfg.SeparateInvoiceParties = _sepParties.Checked;
        _cfg.AutoOrganize = _auto.Checked; // persist the auto-move state
        if (_lang.SelectedIndex >= 0) _cfg.Language = L.Languages[_lang.SelectedIndex].Code;
        try { _cfg.Save(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "AI File Butler"); return; }

        _watcher.Auto = _auto.Checked;
        try { Startup.Set(_startup.Checked); } catch { }
        Close();
    }
}

public enum WelcomeChoice { KeepManual, OpenSettings, EnableAuto }

/// <summary>First-run welcome — explains the app and keeps it safe (manual) until
/// the user opts in. Returns the user's choice via <see cref="Choice"/>.</summary>
public sealed class WelcomeForm : Form
{
    public WelcomeChoice Choice { get; private set; } = WelcomeChoice.KeepManual;

    public WelcomeForm()
    {
        Text = L.S("welcome_title");
        Icon = AppArt.Load();
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(580, 600);
        MinimumSize = new Size(480, 480);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10f);
        TopMost = true;

        var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(16, 14, 16, 0), WrapContents = false };
        try { header.Controls.Add(new PictureBox { Image = AppArt.Load().ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(48, 48), Margin = new Padding(0, 0, 12, 0) }); } catch { }
        header.Controls.Add(new Label { Text = L.S("welcome_title"), AutoSize = true, ForeColor = Color.FromArgb(43, 139, 234), Font = new Font("Segoe UI", 15f, FontStyle.Bold), Margin = new Padding(0, 8, 0, 0) });

        var body = new TextBox
        {
            Text = L.S("welcome_body").Replace("\n", "\r\n"), Dock = DockStyle.Fill, Multiline = true,
            ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
            BackColor = Color.White, TabStop = false,
        };
        var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 6, 20, 6) };
        pad.Controls.Add(body);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 56, Padding = new Padding(12, 10, 12, 10) };
        var openBtn = Btn(L.S("welcome_open"), WelcomeChoice.OpenSettings, true);
        bar.Controls.Add(openBtn);
        bar.Controls.Add(Btn(L.S("welcome_close"), WelcomeChoice.KeepManual, false));

        Controls.Add(pad);
        Controls.Add(header);
        Controls.Add(bar);
        AcceptButton = openBtn;
        Shown += (_, _) => openBtn.Focus();
    }

    private Button Btn(string text, WelcomeChoice choice, bool primary)
    {
        var b = new Button
        {
            Text = text, AutoSize = true, Padding = new Padding(10, 5, 10, 5), FlatStyle = FlatStyle.Flat,
            Margin = new Padding(4), Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9.5f, primary ? FontStyle.Bold : FontStyle.Regular),
            BackColor = primary ? Color.FromArgb(43, 139, 234) : Color.White,
            ForeColor = primary ? Color.White : Color.Black,
        };
        b.FlatAppearance.BorderColor = primary ? Color.FromArgb(43, 139, 234) : Color.LightGray;
        b.Click += (_, _) => { Choice = choice; Close(); };
        return b;
    }
}

/// <summary>A simple localized Help window explaining how the program works.</summary>
public sealed class HelpForm : Form
{
    public HelpForm()
    {
        Text = L.S("help_title");
        Icon = AppArt.Load();
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(560, 560);
        MinimumSize = new Size(420, 380);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10f);

        var title = new Label
        {
            Text = L.S("help_title"), Dock = DockStyle.Top, Height = 48,
            ForeColor = Color.FromArgb(43, 139, 234), Padding = new Padding(16, 12, 16, 0),
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
        };
        var body = new TextBox
        {
            Text = L.S("help_body").Replace("\n", "\r\n"), Dock = DockStyle.Fill, Multiline = true,
            ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
            BackColor = Color.White, Margin = new Padding(16), Padding = new Padding(16), TabStop = false,
        };
        var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 4, 16, 8) };
        pad.Controls.Add(body);
        var close = new Button { Text = "OK", Dock = DockStyle.Bottom, Height = 40, DialogResult = DialogResult.OK };
        Controls.Add(pad);
        Controls.Add(title);
        Controls.Add(close);
    }
}
