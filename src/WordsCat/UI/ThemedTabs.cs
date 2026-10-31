namespace ReadDock;

internal sealed class ThemedTabs : TabControl
{
    public ThemedTabs()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        SizeMode = TabSizeMode.Fixed; ItemSize = new Size(160, 42);
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e); ItemSize = new Size(150 * DeviceDpi / 96, 42 * DeviceDpi / 96);
    }
    protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Ui.Surface); e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        for (int i = 0; i < TabCount; i++)
        {
            var r = GetTabRect(i); bool selected = i == SelectedIndex;
            r.Inflate(-3 * DeviceDpi / 96, -4 * DeviceDpi / 96);
            using var path = Ui.RoundPath(r, 10 * DeviceDpi / 96f);
            using var fill = new SolidBrush(selected ? Ui.Blend(Ui.Surface, Ui.Teal, Ui.IsDark ? .22f : .10f) : Ui.Surface); e.Graphics.FillPath(fill, path);
            Color foreground = selected ? Ui.IsDark ? Ui.Blend(Ui.Teal, Ui.Ink, .4f) : Ui.Teal : Ui.Muted;
            // Keep selected text readable even with a dark custom accent.
            Color textColor = selected ? Ui.Ink : Ui.Muted;
            string glyph = TabPages[i].Tag as string ?? "";
            var textBounds = r;
            if (glyph.Length > 0)
            {
                int icon = 17 * DeviceDpi / 96, gap = 8 * DeviceDpi / 96, inset = 10 * DeviceDpi / 96;
                int textWidth = Math.Min(TextRenderer.MeasureText(TabPages[i].Text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width, Math.Max(0, r.Width - inset * 2 - icon - gap));
                int start = r.Left + (r.Width - icon - gap - textWidth) / 2;
                UiGlyph.Draw(e.Graphics, glyph, new Rectangle(start, r.Top + (r.Height - icon) / 2, icon, icon), foreground);
                textBounds = new Rectangle(start + icon + gap, r.Top, textWidth, r.Height);
            }
            TextRenderer.DrawText(e.Graphics, TabPages[i].Text, Font, textBounds, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }
    }
}
