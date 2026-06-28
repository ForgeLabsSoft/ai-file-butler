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
    private readonly CheckBox _dark = new();
    private readonly NumericUpDown _poll = new();
    private readonly NumericUpDown _minAge = new();
    private readonly NumericUpDown _review = new();
    private readonly ComboBox _musicBy = new();
    private readonly ComboBox _movieBy = new();
    private readonly ComboBox _photoBy = new();
    private readonly CheckBox _sepParties = new();
    private readonly ListBox _rulesList = new();
    private readonly TextBox _ruleMatch = new();
    private readonly TextBox _ruleFolder = new();

    private static readonly string[] MusicOpts = { "artist", "genre", "year", "alpha", "none" };
    private static readonly string[] MovieOpts = { "genre", "actor", "year", "date", "location", "alpha", "none" };
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
        EnableDrop(this); // drag & drop files anywhere to organize them now
        Theme.IsDark = _cfg.DarkMode;
        Theme.Apply(this);
    }

    private void EnableDrop(Control c)
    {
        if (c is not (TextBox or ComboBox or ListBox or NumericUpDown))
        {
            c.AllowDrop = true;
            c.DragEnter += (_, e) =>
            { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
            c.DragDrop += OnFilesDropped;
        }
        foreach (Control child in c.Controls) EnableDrop(child);
    }

    private void OnFilesDropped(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files) return;
        var real = files.Where(File.Exists).ToArray();
        if (real.Length == 0) return;
        Task.Run(() => _watcher.OrganizeDropped(real)); // off the UI thread (AI is slow)
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

        // drop hint (lands at the very top of the form)
        var dropHint = new Label
        {
            Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Accent, BackColor = Color.FromArgb(238, 245, 255),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        };
        Reg(dropHint, "drop_hint");
        Controls.Add(dropHint);

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
        var bWrap = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, AutoSize = true };
        _auto.AutoSize = true; _startup.AutoSize = true; _dark.AutoSize = true;
        _dark.CheckedChanged += (_, _) => { Theme.IsDark = _dark.Checked; Theme.Apply(this); };
        bWrap.Controls.Add(Reg(_auto, "auto"), 0, 0);
        bWrap.Controls.Add(Reg(_startup, "startup"), 0, 1);
        bWrap.Controls.Add(Reg(_dark, "dark_mode"), 0, 2);
        bWrap.Controls.Add(grid, 0, 3);
        bWrap.Controls.Add(rev, 0, 4);
        gBehavior.Controls.Add(bWrap);
        gBehavior.Height = 188;
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

        // my rules (keyword -> folder)
        var gRules = Group("rules");
        var rWrap = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, AutoSize = true };
        rWrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rWrap.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _rulesList.Dock = DockStyle.Fill; _rulesList.IntegralHeight = false; _rulesList.Height = 90;
        rWrap.Controls.Add(_rulesList, 0, 0);
        rWrap.Controls.Add(Reg(MakeButton("", (_, _) => { if (_rulesList.SelectedIndex >= 0) _rulesList.Items.RemoveAt(_rulesList.SelectedIndex); }, false), "remove"), 1, 0);
        var addRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, AutoSize = true };
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _ruleMatch.Dock = DockStyle.Fill; _ruleFolder.Dock = DockStyle.Fill;
        addRow.Controls.Add(_ruleMatch, 0, 0);
        addRow.Controls.Add(_ruleFolder, 1, 0);
        addRow.Controls.Add(Reg(MakeButton("", (_, _) => AddRule(), false), "rule_add"), 2, 0);
        rWrap.Controls.Add(addRow, 0, 1); rWrap.SetColumnSpan(addRow, 2);
        rWrap.Controls.Add(Reg(new Label { AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(2, 4, 0, 0) }, "rule_hint"), 0, 2);
        rWrap.SetColumnSpan(rWrap.GetControlFromPosition(0, 2)!, 2);
        gRules.Controls.Add(rWrap);
        gRules.Height = 200;
        root.Controls.Add(gRules);
    }

    private void AddRule()
    {
        var m = _ruleMatch.Text.Trim();
        var f = _ruleFolder.Text.Trim();
        if (m.Length == 0 || f.Length == 0) return;
        _rulesList.Items.Add(new RuleEntry { Match = m, Folder = f });
        _ruleMatch.Clear(); _ruleFolder.Clear();
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
            Tag = primary ? "primary" : null,
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
        _dark.Checked = _cfg.DarkMode;
        _poll.Value = Math.Clamp(_cfg.PollIntervalSeconds, (int)_poll.Minimum, (int)_poll.Maximum);
        _minAge.Value = Math.Clamp(_cfg.MinAgeSeconds, (int)_minAge.Minimum, (int)_minAge.Maximum);
        _review.Value = Math.Clamp(_cfg.ReviewThreshold, (int)_review.Minimum, (int)_review.Maximum);
        _musicBy.Tag = _cfg.MusicBy; _movieBy.Tag = _cfg.MovieBy; _photoBy.Tag = _cfg.ImageBy;
        _sepParties.Checked = _cfg.SeparateInvoiceParties;
        _rulesList.Items.Clear();
        foreach (var r in _cfg.Rules) _rulesList.Items.Add(r);
        var idx = Array.FindIndex(L.Languages, l => l.Code == L.Lang);
        _lang.SelectedIndex = idx < 0 ? 0 : idx;
    }

    private void Localize()
    {
        foreach (var (c, key) in _loc) c.Text = L.S(key);
        _ruleMatch.PlaceholderText = L.S("rule_match");
        _ruleFolder.PlaceholderText = L.S("rule_folder");
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
        _cfg.Rules = _rulesList.Items.Cast<RuleEntry>().ToList();
        _cfg.AutoOrganize = _auto.Checked; // persist the auto-move state
        _cfg.DarkMode = _dark.Checked; Theme.IsDark = _dark.Checked;
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
        Theme.Apply(this);
    }

    private Button Btn(string text, WelcomeChoice choice, bool primary)
    {
        var b = new Button
        {
            Text = text, AutoSize = true, Padding = new Padding(10, 5, 10, 5), FlatStyle = FlatStyle.Flat,
            Margin = new Padding(4), Cursor = Cursors.Hand, Tag = primary ? "primary" : null,
            Font = new Font("Segoe UI", 9.5f, primary ? FontStyle.Bold : FontStyle.Regular),
            BackColor = primary ? Color.FromArgb(43, 139, 234) : Color.White,
            ForeColor = primary ? Color.White : Color.Black,
        };
        b.FlatAppearance.BorderColor = primary ? Color.FromArgb(43, 139, 234) : Color.LightGray;
        b.Click += (_, _) => { Choice = choice; Close(); };
        return b;
    }
}

/// <summary>Enroll people by example: add a few photos of a person and the app
/// learns their face, then sorts matching photos into People/&lt;Name&gt;.</summary>
public sealed class PeopleForm : Form
{
    private readonly ListBox _list = new();
    private readonly TextBox _name = new();
    private readonly Button _add;
    private List<string> _names = new();

    public PeopleForm()
    {
        Text = L.S("people_title");
        Icon = AppArt.Load();
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(560, 520);
        MinimumSize = new Size(440, 380);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9.5f);

        var hint = new Label
        {
            Text = L.S("ppl_hint"), Dock = DockStyle.Top, Height = 44, ForeColor = Color.Gray,
            Padding = new Padding(14, 10, 14, 0),
        };
        _list.Dock = DockStyle.Fill;
        _list.IntegralHeight = false;

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 2, RowCount = 2, AutoSize = true, Padding = new Padding(12, 8, 12, 10) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _name.Dock = DockStyle.Fill; _name.PlaceholderText = L.S("ppl_name");
        _add = new Button
        {
            Text = L.S("ppl_add"), AutoSize = true, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Padding = new Padding(10, 4, 10, 4), Tag = "primary", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(43, 139, 234), ForeColor = Color.White,
        };
        _add.FlatAppearance.BorderColor = Color.FromArgb(43, 139, 234);
        _add.Click += (_, _) => AddPhoto();
        var remove = new Button
        {
            Text = L.S("ppl_remove"), AutoSize = true, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 6, 0, 0),
        };
        remove.FlatAppearance.BorderColor = Color.LightGray;
        remove.Click += (_, _) =>
        {
            if (_list.SelectedIndex >= 0 && _list.SelectedIndex < _names.Count)
            { People.Remove(_names[_list.SelectedIndex]); Refresh3(); }
        };
        bottom.Controls.Add(_name, 0, 0);
        bottom.Controls.Add(_add, 1, 0);
        bottom.Controls.Add(remove, 0, 1);

        Controls.Add(_list);
        Controls.Add(bottom);
        Controls.Add(hint);

        // one-time model download banner (face model is not bundled)
        var dlBtn = new Button
        {
            Text = L.S("ppl_download"), AutoSize = true, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Tag = "primary", Padding = new Padding(10, 4, 10, 4), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(43, 139, 234), ForeColor = Color.White,
        };
        dlBtn.FlatAppearance.BorderColor = Color.FromArgb(43, 139, 234);
        var dlLbl = new Label { Text = L.S("ppl_dl_hint"), Dock = DockStyle.Top, Height = 22, AutoSize = false };
        var dlFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        dlFlow.Controls.Add(dlBtn);
        var dlBanner = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Color.FromArgb(255, 248, 225), Padding = new Padding(12, 8, 12, 8), Visible = !FaceRecognizer.ModelReady };
        dlBanner.Controls.Add(dlFlow);
        dlBanner.Controls.Add(dlLbl);
        dlBtn.Click += (_, _) =>
        {
            dlBtn.Enabled = false; dlBtn.Text = "…";
            Task.Run(FaceRecognizer.DownloadModel).ContinueWith(t =>
            {
                if (IsDisposed) return;
                BeginInvoke(() =>
                {
                    dlBtn.Enabled = true; dlBtn.Text = L.S("ppl_download");
                    if (t.Result) dlBanner.Visible = false;
                    else MessageBox.Show(this, L.S("ppl_dl_fail"), Text);
                });
            });
        };
        Controls.Add(dlBanner);

        Load += (_, _) => Refresh3();
        Theme.Apply(this);
    }

    private void Refresh3()
    {
        _names = People.List().Select(p => p.Name).ToList();
        _list.Items.Clear();
        foreach (var (name, count) in People.List())
            _list.Items.Add($"{name}   ({count})");
    }

    private void AddPhoto()
    {
        if (!FaceRecognizer.ModelReady) { MessageBox.Show(this, L.S("ppl_dl_hint"), Text); return; }
        var nm = _name.Text.Trim();
        if (nm.Length == 0) { _name.Focus(); return; }
        using var dlg = new OpenFileDialog
        { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.heic;*.tif;*.tiff" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var path = dlg.FileName;
        _add.Enabled = false; _add.Text = "…";
        Task.Run(() => FaceRecognizer.EmbedDominantFace(path)).ContinueWith(t =>
        {
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                _add.Enabled = true; _add.Text = L.S("ppl_add");
                if (t.Result is float[] emb)
                {
                    People.Enroll(nm, emb); Refresh3();
                    MessageBox.Show(this, string.Format(L.S("ppl_added"), nm), Text);
                }
                else MessageBox.Show(this, L.S("ppl_noface"), Text);
            });
        });
    }
}

/// <summary>Shows the move history and lets the user undo any individual move.</summary>
public sealed class HistoryForm : Form
{
    private readonly ListView _list = new();

    public HistoryForm()
    {
        Text = L.S("history_title");
        Icon = AppArt.Load();
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(760, 520);
        MinimumSize = new Size(520, 360);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9.5f);

        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.Dock = DockStyle.Fill;
        _list.Columns.Add(L.S("hist_file"), 240);
        _list.Columns.Add(L.S("hist_dest"), 340);
        _list.Columns.Add(L.S("hist_when"), 140);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 50, Padding = new Padding(10, 8, 10, 8) };
        var undo = new Button
        {
            Text = L.S("hist_undo"), AutoSize = true, Padding = new Padding(12, 5, 12, 5),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(43, 139, 234), ForeColor = Color.White, Tag = "primary",
        };
        undo.FlatAppearance.BorderColor = Color.FromArgb(43, 139, 234);
        undo.Click += (_, _) => UndoSelected();
        bar.Controls.Add(undo);

        Controls.Add(_list);
        Controls.Add(bar);
        Load += (_, _) => Refresh2();
        Theme.Apply(this);
    }

    private void Refresh2()
    {
        _list.Items.Clear();
        var hist = Organizer.History();
        foreach (var h in hist)
        {
            var when = DateTimeOffset.TryParse(h.Ts, out var dt) ? dt.LocalDateTime.ToString("g") : h.Ts;
            var item = new ListViewItem(new[] { Path.GetFileName(h.To), TrimMid(h.To), when }) { Tag = h };
            _list.Items.Add(item);
        }
        if (hist.Count == 0)
            _list.Items.Add(new ListViewItem(new[] { L.S("hist_empty"), "", "" }));
    }

    private void UndoSelected()
    {
        var picked = _list.SelectedItems.Cast<ListViewItem>()
            .Select(i => i.Tag as HistoryItem).Where(h => h is not null).Cast<HistoryItem>().ToList();
        foreach (var h in picked) Organizer.UndoOne(h);
        Refresh2();
    }

    private static string TrimMid(string p) => p.Length <= 60 ? p : p[..28] + "…" + p[^30..];
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
        Theme.Apply(this);
    }
}
