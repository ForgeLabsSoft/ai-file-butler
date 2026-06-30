using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AIFileButler;

/// <summary>Light/dark theming for all windows. WinForms has no built-in dark
/// mode, so we recolor controls (and the title bar via DWM) ourselves.</summary>
internal static class Theme
{
    public static bool IsDark;

    public static readonly Color Accent = Color.FromArgb(43, 139, 234);

    private static readonly Color DBg = Color.FromArgb(32, 33, 40);
    private static readonly Color DInput = Color.FromArgb(50, 52, 63);
    private static readonly Color DText = Color.FromArgb(232, 233, 240);
    private static readonly Color DSub = Color.FromArgb(150, 153, 165);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void Apply(Form f)
    {
        f.BackColor = IsDark ? DBg : Color.White;
        f.ForeColor = IsDark ? DText : Color.Black;
        TitleBar(f);
        foreach (Control c in f.Controls) Style(c);
    }

    private static void TitleBar(Form f)
    {
        void Set()
        {
            int v = IsDark ? 1 : 0;
            try { DwmSetWindowAttribute(f.Handle, 20, ref v, sizeof(int)); } catch { }
        }
        if (f.IsHandleCreated) Set(); else f.HandleCreated += (_, _) => Set();
    }

    private static void Style(Control c)
    {
        switch (c)
        {
            case TextBox or ComboBox or NumericUpDown or ListBox or ListView:
                c.BackColor = IsDark ? DInput : Color.White;
                c.ForeColor = IsDark ? DText : Color.Black;
                break;
            case Button b:
                if ((b.Tag as string) == "primary")
                {
                    b.BackColor = Accent; b.ForeColor = Color.White;
                    b.FlatAppearance.BorderColor = Accent;
                }
                else
                {
                    b.BackColor = IsDark ? DInput : Color.White;
                    b.ForeColor = IsDark ? DText : Color.Black;
                    b.FlatAppearance.BorderColor = IsDark ? DInput : Color.LightGray;
                }
                break;
            case Label lbl:
                lbl.BackColor = Color.Transparent;
                if (lbl.ForeColor != Accent)
                    lbl.ForeColor = IsDark
                        ? (lbl.ForeColor == Color.Gray ? DSub : DText)
                        : (lbl.ForeColor == DSub ? Color.Gray : lbl.ForeColor);
                break;
            case GroupBox:
                c.ForeColor = IsDark ? DText : Color.Black;
                c.BackColor = Color.Transparent;
                break;
            case Panel or TableLayoutPanel or FlowLayoutPanel:
                c.BackColor = IsDark ? DBg : Color.White;
                c.ForeColor = IsDark ? DText : Color.Black;
                break;
        }
        foreach (Control ch in c.Controls) Style(ch);
    }

    /// <summary>Make a details ListView look modern: taller rows, a flat
    /// borderless header, accent row selection — instead of the boxy Win32 grid.</summary>
    public static void ModernList(ListView lv)
    {
        lv.View = View.Details;
        lv.FullRowSelect = true;
        lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        lv.BorderStyle = BorderStyle.None;
        lv.Font = new Font("Segoe UI", 9.75f);
        lv.OwnerDraw = true;
        lv.SmallImageList = new ImageList { ImageSize = new Size(1, 30) }; // forces ~30px rows

        Color headBg = IsDark ? Color.FromArgb(46, 47, 57) : Color.FromArgb(245, 246, 248);
        Color headFg = IsDark ? Color.FromArgb(168, 170, 182) : Color.FromArgb(92, 94, 102);
        Color rowBg = IsDark ? DInput : Color.White;
        Color line = IsDark ? Color.FromArgb(58, 60, 72) : Color.FromArgb(235, 236, 240);
        var headFont = new Font(lv.Font, FontStyle.Bold);
        const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis;

        lv.DrawColumnHeader += (_, e) =>
        {
            using var b = new SolidBrush(headBg);
            e.Graphics.FillRectangle(b, e.Bounds);
            using var p = new Pen(line);
            e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            var r = e.Bounds; r.X += 10; r.Width -= 12;
            TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", headFont, r, headFg, flags);
        };
        lv.DrawItem += (_, _) => { }; // required so subitems own-draw in Details view
        lv.DrawSubItem += (_, e) =>
        {
            bool sel = e.Item!.Selected;
            using (var b = new SolidBrush(sel ? Accent : rowBg)) e.Graphics.FillRectangle(b, e.Bounds);
            if (!sel)
            {
                using var p = new Pen(line);
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
            var fg = sel ? Color.White : e.Item.ForeColor;
            var r = e.Bounds; r.X += 10; r.Width -= 12;
            TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", lv.Font, r, fg, flags);
        };
    }

    /// <summary>Give a tray/context menu a flat, modern look (no 3-D borders,
    /// accent hover, comfortable spacing) instead of the dated Win32 default.</summary>
    public static void StyleMenu(ToolStrip menu)
    {
        menu.Renderer = new ModernMenuRenderer();
        menu.Font = new Font("Segoe UI", 9.75f);
        menu.ImageScalingSize = new Size(18, 18);
        menu.Padding = new Padding(6);
    }

    private sealed class ModernColors : ProfessionalColorTable
    {
        private static Color Bg => IsDark ? Color.FromArgb(38, 39, 48) : Color.White;
        public override Color ToolStripDropDownBackground => Bg;
        public override Color ImageMarginGradientBegin => Bg;
        public override Color ImageMarginGradientMiddle => Bg;
        public override Color ImageMarginGradientEnd => Bg;
        public override Color MenuItemSelected => Accent;
        public override Color MenuItemSelectedGradientBegin => Accent;
        public override Color MenuItemSelectedGradientEnd => Accent;
        public override Color MenuItemPressedGradientBegin => Accent;
        public override Color MenuItemPressedGradientEnd => Accent;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuBorder => IsDark ? Color.FromArgb(64, 66, 78) : Color.FromArgb(224, 224, 228);
        public override Color SeparatorDark => IsDark ? Color.FromArgb(64, 66, 78) : Color.FromArgb(232, 232, 236);
        public override Color SeparatorLight => Bg;
    }

    private sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
    {
        public ModernMenuRenderer() : base(new ModernColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // White text on the accent highlight; otherwise follow the theme.
            if (e.Item.Selected && e.Item.Enabled)
                e.TextColor = Color.White;
            else if (!e.Item.Enabled)
                e.TextColor = IsDark ? Color.FromArgb(140, 142, 152) : Color.Gray;
            else
                e.TextColor = IsDark ? DText : Color.FromArgb(28, 28, 32);
            base.OnRenderItemText(e);
        }
    }
}
