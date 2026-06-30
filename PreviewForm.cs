using System.Drawing;
using System.Windows.Forms;

namespace AIFileButler;

/// <summary>
/// Shows the moves the Butler is about to make and lets the user tick/untick each
/// before anything is touched — the trust gap most AI organizers leave open. The
/// accepted subset is exposed via <see cref="Accepted"/>.
/// </summary>
public sealed class PreviewForm : Form
{
    private readonly ListView _list = new();
    public List<Watcher.Pending> Accepted { get; private set; } = new();

    public PreviewForm(List<Watcher.Pending> moves)
    {
        Text = L.S("prev_title");
        Icon = AppArt.Load();
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(900, 540);
        MinimumSize = new Size(640, 360);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9.5f);

        var header = new Label
        {
            Text = string.Format(L.S("prev_header"), moves.Count), Dock = DockStyle.Top, Height = 40,
            Padding = new Padding(14, 11, 14, 0), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
        };

        _list.View = View.Details;
        _list.CheckBoxes = true;
        _list.FullRowSelect = true;
        _list.GridLines = false;
        _list.Dock = DockStyle.Fill;
        _list.Font = new Font("Segoe UI", 9.5f);
        _list.Columns.Add(L.S("prev_file"), 230);
        _list.Columns.Add(L.S("prev_newname"), 250);
        _list.Columns.Add(L.S("prev_folder"), 170);
        _list.Columns.Add(L.S("prev_why"), 200);
        foreach (var m in moves)
        {
            var folder = TrimMid(Path.GetDirectoryName(m.Plan.Dst) ?? "");
            var item = new ListViewItem(new[] { Path.GetFileName(m.Plan.Src), m.Plan.DstName, folder, m.Plan.Reason })
            { Tag = m, Checked = true };
            if (m.Plan.Category == "misc" || m.Plan.Confidence < 0.55) item.ForeColor = Color.DarkOrange;
            _list.Items.Add(item);
        }

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 52, Padding = new Padding(12, 9, 12, 9) };
        var apply = new Button
        {
            Text = L.S("prev_apply"), AutoSize = true, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Padding = new Padding(14, 5, 14, 5), Tag = "primary", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(43, 139, 234), ForeColor = Color.White,
        };
        apply.FlatAppearance.BorderColor = Color.FromArgb(43, 139, 234);
        apply.Click += (_, _) =>
        {
            Accepted = _list.CheckedItems.Cast<ListViewItem>().Select(i => (Watcher.Pending)i.Tag!).ToList();
            DialogResult = DialogResult.OK; Close();
        };
        var cancel = new Button { Text = L.S("cancel"), AutoSize = true, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Padding = new Padding(14, 5, 14, 5), Margin = new Padding(8, 0, 0, 0) };
        cancel.FlatAppearance.BorderColor = Color.LightGray;
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        // quick select-all / none
        var none = new Button { Text = L.S("prev_none_btn"), AutoSize = true, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Padding = new Padding(12, 5, 12, 5), Margin = new Padding(8, 0, 0, 0) };
        none.FlatAppearance.BorderColor = Color.LightGray;
        none.Click += (_, _) => { bool any = _list.CheckedItems.Count > 0; foreach (ListViewItem it in _list.Items) it.Checked = !any; };
        bar.Controls.Add(apply);
        bar.Controls.Add(cancel);
        bar.Controls.Add(none);

        Controls.Add(_list);
        Controls.Add(header);
        Controls.Add(bar);
        AcceptButton = apply; CancelButton = cancel;
        Theme.Apply(this);
    }

    private static string TrimMid(string p) => p.Length <= 40 ? p : "…" + p[^39..];
}
