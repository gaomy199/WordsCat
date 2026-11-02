using System.Drawing.Drawing2D;

namespace ReadDock;

// CheckBox supplies native keyboard and accessibility toggle semantics.
internal sealed class SlideSwitch : CheckBox
{
    public SlideSwitch()
    {
        AutoSize = false; Dock = DockStyle.Fill; Font = Ui.Font(9);
        AccessibleRole = AccessibleRole.CheckButton;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        CheckedChanged += (_, _) => Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Ui.Surface);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96f;
        float width = 40 * scale, height = 22 * scale;
        var track = new RectangleF(ClientSize.Width - width - 5 * scale, (ClientSize.Height - height) / 2, width, height);
        using var path = Ui.RoundPath(track, height / 2);
        using var fill = new SolidBrush(Enabled && Checked ? Ui.Teal : Ui.Border);
        e.Graphics.FillPath(fill, path);
        float diameter = height - 6 * scale;
        using var thumb = new SolidBrush(Enabled ? Color.White : Ui.Surface);
        e.Graphics.FillEllipse(thumb, Checked ? track.Right - diameter - 3 * scale : track.Left + 3 * scale, track.Top + 3 * scale, diameter, diameter);
        var textBounds = new Rectangle(2, 0, Math.Max(0, (int)track.Left - 12), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, Enabled ? Ui.Ink : Ui.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle, Ui.Ink, BackColor);
    }
}
