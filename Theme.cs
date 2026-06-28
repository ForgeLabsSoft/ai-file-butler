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
}
