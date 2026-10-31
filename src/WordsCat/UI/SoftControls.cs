using System.Drawing.Drawing2D;

namespace ReadDock;

// Preserve the native dropdown and editable child, but paint its chrome with the
// app palette. Windows' flat combo border/arrow otherwise stay white in dark mode.
internal sealed class SoftComboBox : ComboBox
{
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != 0x000F || !IsHandleCreated || IsDisposed || Width < 8 || Height < 8) return;
        using var graphics = Graphics.FromHwnd(Handle);
        float scale = DeviceDpi / 96f;
        using var border = new Pen(Ui.Surface, Math.Max(2, 3 * scale));
        graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        int arrowWidth = (int)(22 * scale);
        using var background = new SolidBrush(Ui.Surface); graphics.FillRectangle(background, Width - arrowWidth, 0, arrowWidth, Height);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Enabled ? Ui.Muted : Ui.Border, 1.5f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float center = Width - arrowWidth / 2f, middle = Height / 2f;
        graphics.DrawLines(pen, [new PointF(center - 3 * scale, middle - scale), new PointF(center, middle + 2 * scale), new PointF(center + 3 * scale, middle - scale)]);
    }
}

internal class RoundedPanel : Panel
{
    public bool Outline { get; set; } = true;
    public int Radius { get; set; } = 12;
    public RoundedPanel()
    {
        Tag = "surface";
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Ui.Paper);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Ui.RoundPath(new RectangleF(.5f, .5f, Width - 1, Height - 1), Radius * DeviceDpi / 96f);
        using var fill = new SolidBrush(Ui.Surface); e.Graphics.FillPath(fill, path);
        if (Outline)
        {
            using var pen = new Pen(ContainsFocus ? Ui.Teal : Ui.Border, ContainsFocus ? 1.5f : 1);
            e.Graphics.DrawPath(pen, path);
        }
    }
}

// Native editors retain their keyboard, IME, password and accessibility behavior.
// The frame supplies a consistent boundary and keeps short choices compact.
internal sealed class FieldFrame : RoundedPanel
{
    public Control Editor { get; }
    protected override Size DefaultSize => new(240, 38);
    public FieldFrame(Control editor, int maximumWidth = 0)
    {
        Editor = editor; Radius = 8; Dock = DockStyle.Fill; Margin = new Padding(0, 0, 0, 8);
        if (maximumWidth > 0) MaximumSize = new Size(maximumWidth, 0);
        editor.Dock = DockStyle.None; editor.Anchor = AnchorStyles.None; editor.Margin = Padding.Empty;
        if (editor is TextBoxBase text) text.BorderStyle = BorderStyle.None;
        if (editor is NumericUpDown number) number.BorderStyle = BorderStyle.None;
        Controls.Add(editor);
        editor.Enter += (_, _) => Invalidate(); editor.Leave += (_, _) => Invalidate();
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = Math.Max(8, 12 * DeviceDpi / 96), vertical = Math.Max(5, 7 * DeviceDpi / 96);
        int height = Editor switch
        {
            ComboBox combo => combo.PreferredHeight,
            TextBoxBase { Multiline: true } or TrackBar => Math.Max(1, ClientSize.Height - vertical * 2),
            TextBox text => text.PreferredHeight,
            UpDownBase number => number.PreferredHeight,
            _ => Editor.Font.Height + vertical
        };
        Editor.SetBounds(inset, Math.Max(0, (ClientSize.Height - height) / 2), Math.Max(1, ClientSize.Width - inset * 2), height);
    }
}

internal static class UiGlyph
{
    public static string For(string text) => text switch
    {
        "划词翻译" => "selection", "截图翻译" => "capture", "收藏所选词" or "收藏单词" or "保存到单词本" => "bookmark",
        "粘贴原文" => "paste", "打开单词本" => "book", "退出" => "exit",
        "收起侧边栏" => "sidebar-close", "展开侧边栏" => "sidebar-open",
        "设置" => "settings", "翻译当前原文" => "translate", "交换" => "swap", "追问" => "chat", "×" => "close", _ => ""
    };
    public static void Draw(Graphics graphics, string name, Rectangle bounds, Color color)
    {
        if (name.Length == 0) return;
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TranslateTransform(bounds.X, bounds.Y); graphics.ScaleTransform(bounds.Width / 20f, bounds.Height / 20f);
        using var pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var fill = new SolidBrush(color);
        switch (name)
        {
            case "sidebar-close": case "sidebar-open":
                graphics.DrawRectangle(pen, 2, 3, 16, 14); graphics.DrawLine(pen, 7, 3, 7, 17);
                float direction = name == "sidebar-close" ? -1 : 1;
                graphics.DrawLines(pen, [new PointF(12 - direction * 2, 7), new PointF(12 + direction, 10), new PointF(12 - direction * 2, 13)]); break;
            case "paste":
                graphics.DrawLines(pen, [new PointF(6, 4), new PointF(3, 4), new PointF(3, 18), new PointF(17, 18), new PointF(17, 4), new PointF(14, 4)]);
                graphics.DrawRectangle(pen, 6, 2, 8, 5); graphics.DrawLine(pen, 7, 11, 13, 11); graphics.DrawLine(pen, 7, 14, 12, 14); break;
            case "exit":
                graphics.DrawLines(pen, [new PointF(8, 3), new PointF(3, 3), new PointF(3, 17), new PointF(8, 17)]);
                graphics.DrawLine(pen, 8, 10, 18, 10); graphics.DrawLines(pen, [new PointF(14, 6), new PointF(18, 10), new PointF(14, 14)]); break;
            case "capture":
                graphics.DrawRectangle(pen, 2, 5, 16, 12); graphics.DrawEllipse(pen, 7, 8, 6, 6); graphics.DrawLine(pen, 6, 5, 8, 2); graphics.DrawLine(pen, 8, 2, 13, 2); break;
            case "bookmark": case "reading":
                graphics.DrawPolygon(pen, [new PointF(5, 2), new PointF(15, 2), new PointF(15, 18), new PointF(10, 14), new PointF(5, 18)]); break;
            case "swap": case "translate":
                graphics.DrawLines(pen, [new PointF(3, 6), new PointF(17, 6), new PointF(13, 2)]);
                graphics.DrawLines(pen, [new PointF(17, 14), new PointF(3, 14), new PointF(7, 18)]); break;
            case "settings":
                graphics.DrawEllipse(pen, 3, 3, 14, 14); graphics.DrawEllipse(pen, 7, 7, 6, 6);
                for (int i = 0; i < 8; i++) { double angle = i * Math.PI / 4; graphics.DrawLine(pen, 10 + 7 * (float)Math.Cos(angle), 10 + 7 * (float)Math.Sin(angle), 10 + 9 * (float)Math.Cos(angle), 10 + 9 * (float)Math.Sin(angle)); } break;
            case "model":
                graphics.DrawRectangle(pen, 3, 3, 14, 11); graphics.DrawLine(pen, 10, 14, 10, 18); graphics.DrawLine(pen, 6, 18, 14, 18);
                graphics.FillEllipse(fill, 6, 7, 2, 2); graphics.FillEllipse(fill, 12, 7, 2, 2); break;
            case "book":
                graphics.DrawRectangle(pen, 3, 3, 14, 14); graphics.DrawLine(pen, 7, 3, 7, 17); graphics.DrawLine(pen, 10, 7, 14, 7); graphics.DrawLine(pen, 10, 11, 14, 11); break;
            case "appearance":
                graphics.DrawEllipse(pen, 2, 2, 16, 16); graphics.FillEllipse(fill, 6, 5, 3, 3); graphics.FillEllipse(fill, 12, 6, 3, 3); graphics.FillEllipse(fill, 5, 11, 3, 3); graphics.DrawArc(pen, 10, 11, 6, 6, 10, 180); break;
            case "selection":
                graphics.DrawLine(pen, 4, 3, 16, 3); graphics.DrawLine(pen, 10, 3, 10, 17); graphics.DrawLine(pen, 6, 17, 14, 17); break;
            case "chat":
                using (var path = Ui.RoundPath(new RectangleF(2, 2, 16, 13), 4)) graphics.DrawPath(pen, path);
                graphics.DrawLines(pen, [new PointF(5, 15), new PointF(5, 18), new PointF(9, 15)]); graphics.DrawLine(pen, 6, 7, 14, 7); graphics.DrawLine(pen, 6, 10, 11, 10); break;
            case "close": graphics.DrawLine(pen, 5, 5, 15, 15); graphics.DrawLine(pen, 15, 5, 5, 15); break;
        }
        graphics.Restore(state);
    }
}
