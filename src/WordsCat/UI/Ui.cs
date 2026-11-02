using System.ComponentModel;

namespace ReadDock;

internal static class Ui
{
    public static Color Ink { get; private set; } = Color.FromArgb(29, 50, 59);
    public static Color Muted { get; private set; } = Color.FromArgb(100, 119, 126);
    public static Color Teal { get; private set; } = Color.FromArgb(20, 114, 101);
    public static Color Paper { get; private set; } = Color.FromArgb(245, 247, 245);
    public static Color Surface { get; private set; } = Color.White;
    public static Color Border { get; private set; } = Color.FromArgb(218, 227, 225);
    public static Color OnAccent { get; private set; } = Color.White;
    public static Image? Logo { get; private set; }
    public static bool HasCustomLogo { get; private set; }
    public static bool IsDark { get; private set; }
    static bool inputTrackingInstalled, keyboardNavigation;
    public static void SetAppearance(AppConfig config)
    {
        bool systemDark = false;
        try { systemDark = Convert.ToInt32(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1)) == 0; } catch { }
        IsDark = config.ThemeMode == "dark" || config.ThemeMode == "system" && systemDark;
        int delta = Math.Clamp(config.BackgroundBrightness, 0, 100) - 50;
        static Color Neutral(int x) { int n = Math.Clamp(x, 0, 255); return Color.FromArgb(n, n, n); }
        Paper = IsDark ? Neutral(22 + delta / 4) : Color.FromArgb(Math.Clamp(244 + delta / 5, 0, 255), Math.Clamp(247 + delta / 5, 0, 255), Math.Clamp(250 + delta / 5, 0, 255));
        Surface = Neutral(IsDark ? 32 + delta / 4 : 252 + delta / 10);
        Ink = IsDark ? Color.FromArgb(230, 237, 239) : Color.FromArgb(29, 50, 59);
        Muted = IsDark ? Color.FromArgb(160, 179, 187) : Color.FromArgb(92, 112, 121);
        Border = IsDark ? Color.FromArgb(65, 76, 87) : Color.FromArgb(218, 225, 232);
        Teal = ParseAccent(config.AccentColor);
        OnAccent = 0.2126 * Teal.R + 0.7152 * Teal.G + 0.0722 * Teal.B > 160 ? Color.FromArgb(16, 30, 36) : Color.White;
        var previous = Logo;
        var custom = LoadLogo(config.LogoFile); HasCustomLogo = custom != null;
        Logo = custom ?? CreateDefaultLogo(); previous?.Dispose();
    }
    public static Color ParseAccent(string hex)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(hex, @"^#[0-9A-Fa-f]{6}$")) throw new InvalidDataException("主题颜色请使用 #RRGGBB 格式，例如 #147265。");
        return ColorTranslator.FromHtml(hex);
    }
    public static Image? LoadLogo(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) return null;
        if (Path.GetExtension(file).Equals(".ico", StringComparison.OrdinalIgnoreCase)) { using var icon = new Icon(file); return icon.ToBitmap(); }
        using var original = Image.FromFile(file);
        if (original.Width > 8192 || original.Height > 8192) throw new InvalidDataException("Logo 图片过大，请使用不超过 8192 像素的图片。");
        return new Bitmap(original);
    }
    public static Font Font(float size = 10, bool bold = false) => new("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
    public static Color Blend(Color a, Color b, float amount) => Color.FromArgb((int)(a.R + (b.R - a.R) * amount), (int)(a.G + (b.G - a.G) * amount), (int)(a.B + (b.B - a.B) * amount));
    public static System.Drawing.Drawing2D.GraphicsPath RoundPath(RectangleF rect, float radius)
    {
        float diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(rect.Width, rect.Height)));
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90); path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90); path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90); path.CloseFigure(); return path;
    }
    public static Button CircleButton(string text, Action action, int size = 36)
    {
        var button = (ThemeButton)Button(text, action, false, size); button.IconOnly = true; button.Height = size;
        button.AccessibleName = text; return button;
    }
    public static void SetButtonGlyph(Button button, string glyph)
    {
        if (button is ThemeButton themed) { themed.Glyph = glyph; themed.Invalidate(); }
    }
    public static Control Card(Control content)
    {
        var panel = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(10), Margin = Padding.Empty, Outline = false, Radius = 14 };
        content.Dock = DockStyle.Fill; panel.Controls.Add(content); return panel;
    }
    public static Button Button(string text, Action action, bool primary = false, int width = 115)
    {
        var b = new ThemeButton { Text = text, Glyph = UiGlyph.For(text), IconOnly = text == "×", AutoSize = false, Width = width, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = primary ? Teal : Surface, ForeColor = primary ? OnAccent : Ink, Font = Font(9), Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0), Tag = primary ? "primary" : "button" };
        b.FlatAppearance.BorderColor = primary ? Teal : Border; b.Click += (_, _) => action(); return b;
    }
    public static Label Label(string text, bool bold = false, float size = 10) => new() { Text = text, UseMnemonic = false, Font = Font(size, bold), ForeColor = bold ? Ink : Muted, AutoSize = false, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
    public static TextBox TextBox(bool multiline = false) => new() { Font = Font(10), BorderStyle = BorderStyle.FixedSingle, Multiline = multiline, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None, BackColor = Surface, ForeColor = Ink, Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 8), AcceptsReturn = multiline };
    public static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = Padding.Empty, Padding = new Padding(0, 2, 0, 2) };
        row.Controls.AddRange(controls); return row;
    }
    public static void Error(IWin32Window owner, Exception ex) => MessageBox.Show(owner, SecretRedactor.Redact(UiText.T(ex.Message)), "单词猫 WordsCat", MessageBoxButtons.OK, MessageBoxIcon.Information);
    public static void OpenFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("文件尚未创建，请先保存一条生词，或在设置中选择已有笔记。");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }
    public static void FinishLayout(Form form)
    {
        if (!inputTrackingInstalled) { Application.AddMessageFilter(new InputModeTracker()); inputTrackingInstalled = true; }
        if (form is not MainWindow)
        {
            var icon = CreateAppIcon(); form.Icon = icon;
            form.Disposed += (_, _) => icon.Dispose();
        }
        // Manual pixel scaling also covers absolute TableLayoutPanel rows/columns.
        // Font point sizes stay unchanged; Windows renders them at the monitor DPI.
        form.AutoScaleMode = AutoScaleMode.None;
        ScaleTree(form, form.DeviceDpi / 96f);
        UiText.Apply(form);
        ApplyTheme(form);
        form.DpiChanged += (_, e) => ScaleTree(form, (float)e.DeviceDpiNew / e.DeviceDpiOld);
        form.Load += (_, _) =>
        {
            var area = Screen.FromControl(form).WorkingArea;
            int width = Math.Max(600, area.Width - 24), height = Math.Max(480, area.Height - 24);
            form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width, width), Math.Min(form.MinimumSize.Height, height));
            form.Size = new Size(Math.Min(form.Width, width), Math.Min(form.Height, height));
            if (form.StartPosition == FormStartPosition.CenterScreen)
                form.Location = new Point(area.Left + (area.Width - form.Width) / 2, area.Top + (area.Height - form.Height) / 2);
        };
    }
    sealed class InputModeTracker : IMessageFilter
    {
        public bool PreFilterMessage(ref Message m)
        {
            bool next = keyboardNavigation;
            if (m.Msg is 0x0201 or 0x0204 or 0x00A1) next = false;
            else if (m.Msg == 0x0100 && (Keys)m.WParam.ToInt32() is Keys.Tab or Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Space or Keys.Enter) next = true;
            if (next != keyboardNavigation)
            {
                keyboardNavigation = next;
                static void RefreshButtons(Control parent)
                {
                    foreach (Control child in parent.Controls) { if (child is Button) child.Invalidate(); RefreshButtons(child); }
                }
                foreach (Form window in Application.OpenForms) RefreshButtons(window);
            }
            return false;
        }
    }
    public static void ApplyTheme(Control control)
    {
        if (control is Button button)
        {
            bool primary = button.Tag as string == "primary";
            button.BackColor = primary ? Teal : Surface; button.ForeColor = primary ? OnAccent : Ink;
            button.FlatAppearance.BorderColor = primary ? Teal : Border;
        }
        else if (control is ComboBox combo)
        {
            combo.BackColor = Surface; combo.ForeColor = Ink; combo.FlatStyle = FlatStyle.Flat;
            if (combo.DropDownStyle == ComboBoxStyle.DropDownList && combo.DrawMode != DrawMode.OwnerDrawFixed)
            {
                combo.DrawMode = DrawMode.OwnerDrawFixed; combo.ItemHeight = Math.Max(22, 24 * combo.DeviceDpi / 96);
                combo.DrawItem += (_, e) =>
                {
                    bool selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
                    using var brush = new SolidBrush(selected ? Teal : Surface); e.Graphics.FillRectangle(brush, e.Bounds);
                    string text = e.Index >= 0 && e.Index < combo.Items.Count ? combo.GetItemText(combo.Items[e.Index]) ?? "" : combo.Text;
                    TextRenderer.DrawText(e.Graphics, text, combo.Font, e.Bounds, selected ? OnAccent : Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                };
            }
        }
        else if (control is TextBoxBase || control is ListBox)
        { control.BackColor = Surface; control.ForeColor = Ink; }
        else if (control is Label label)
        { label.ForeColor = label.Font.Bold ? Ink : Muted; label.BackColor = Color.Transparent; }
        else if (control is DataGridView grid)
        {
            grid.BackgroundColor = Surface; grid.GridColor = Border; grid.EnableHeadersVisualStyles = false;
            grid.DefaultCellStyle.BackColor = Surface; grid.DefaultCellStyle.ForeColor = Ink;
            grid.DefaultCellStyle.SelectionBackColor = Teal; grid.DefaultCellStyle.SelectionForeColor = OnAccent;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Paper; grid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
            grid.RowHeadersDefaultCellStyle.BackColor = Paper; grid.RowHeadersDefaultCellStyle.ForeColor = Ink;
        }
        else if (control is ContextMenuStrip strip)
        { strip.BackColor = Surface; strip.ForeColor = Ink; strip.Renderer = new ToolStripProfessionalRenderer(new ThemeColors()); }
        else
        {
            control.BackColor = control is TabPage || control.Tag as string == "surface" ? Surface : control.Parent?.BackColor ?? Paper;
            control.ForeColor = Ink;
        }
        foreach (Control child in control.Controls) ApplyTheme(child);
        if (control is Form window)
        {
            if (window.IsHandleCreated) SetTitleBar(window.Handle);
            else window.HandleCreated += (_, _) => SetTitleBar(window.Handle);
        }
        control.Invalidate();
    }
    // WinForms' disabled button renderer can retain a light-theme text shadow.
    sealed class ThemeButton : Button
    {
        [DefaultValue("")]
        public string Glyph { get; set; } = "";
        [DefaultValue(false)]
        public bool IconOnly { get; set; }
        bool hover, pressed;
        public ThemeButton() => SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { keyboardNavigation = false; pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnLostFocus(EventArgs e) { hover = pressed = false; Invalidate(); base.OnLostFocus(e); }
        protected override void OnMouseCaptureChanged(EventArgs e) { pressed = false; Invalidate(); base.OnMouseCaptureChanged(e); }
        protected override void OnClick(EventArgs e)
        {
            try { base.OnClick(e); }
            finally { hover = pressed = false; if (!IsDisposed) Invalidate(); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? Paper); e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float scale = DeviceDpi / 96f; bool primary = Tag as string == "primary";
            bool keyboardFocus = Focused && keyboardNavigation;
            Color color = Enabled ? ForeColor : Muted;
            Color background = !Enabled ? Blend(BackColor, Paper, .5f) : pressed ? Blend(BackColor, primary ? Color.Black : Teal, .14f) : hover ? Blend(BackColor, primary ? Color.White : Teal, .10f) : BackColor;
            using var path = RoundPath(new RectangleF(.75f, .75f, Width - 1.5f, Height - 1.5f), IconOnly ? Height / 2f : 9 * scale);
            using var fill = new SolidBrush(background); e.Graphics.FillPath(fill, path);
            using var border = new Pen(keyboardFocus || hover ? Teal : primary ? background : Border, keyboardFocus ? 1.6f : 1); e.Graphics.DrawPath(border, path);
            var textRect = Rectangle.Inflate(ClientRectangle, -(int)(8 * scale), 0);
            if (Glyph.Length > 0)
            {
                int icon = (int)(16 * scale), gap = (int)(7 * scale);
                int textWidth = IconOnly ? 0 : Math.Min(TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width, Math.Max(0, textRect.Width - icon - gap));
                int start = (Width - icon - (IconOnly ? 0 : gap + textWidth)) / 2;
                UiGlyph.Draw(e.Graphics, Glyph, new Rectangle(start, (Height - icon) / 2, icon, icon), !Enabled ? Muted : primary ? OnAccent : hover ? Teal : color);
                textRect = new Rectangle(start + icon + gap, 0, textWidth, Height);
            }
            if (!IconOnly) TextRenderer.DrawText(e.Graphics, Text, Font, textRect, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }
    }
    sealed class ThemeColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Teal;
        public override Color MenuItemSelected => Paper;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
    }
    public static Icon CreateAppIcon()
    {
        if (!HasCustomLogo) return LoadDefaultIcon();
        using var image = new Bitmap(64, 64); using var g = Graphics.FromImage(image);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        if (Logo != null) DrawLogo(g, Logo, new Rectangle(0, 0, 64, 64));
        else
        {
            using var brush = new SolidBrush(Teal); g.FillEllipse(brush, 0, 0, 63, 63);
            using var font = Font(26, true); TextRenderer.DrawText(g, "猫", font, new Rectangle(0, 0, 64, 64), OnAccent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        var h = image.GetHicon();
        try { using var borrowed = Icon.FromHandle(h); return (Icon)borrowed.Clone(); }
        finally { DestroyIcon(h); }
    }
    static Icon LoadDefaultIcon(int size = 0)
    {
        using var stream = typeof(Ui).Assembly.GetManifestResourceStream("WordsCat.DefaultIcon") ?? throw new InvalidDataException("缺少默认图标资源。");
        return size > 0 ? new Icon(stream, size, size) : new Icon(stream);
    }
    public static Image CreateDefaultLogo() { using var icon = LoadDefaultIcon(128); return icon.ToBitmap(); }
    public static void DrawLogo(Graphics graphics, Image logo, Rectangle box)
    {
        float factor = Math.Min((float)box.Width / logo.Width, (float)box.Height / logo.Height);
        int width = Math.Max(1, (int)(logo.Width * factor)), height = Math.Max(1, (int)(logo.Height * factor));
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(logo, new Rectangle(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height));
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    static void SetTitleBar(IntPtr handle) { try { int dark = IsDark ? 1 : 0; DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int)); } catch { } }
    static void ScaleTree(Control control, float scale)
    {
        control.SuspendLayout();
        static Padding S(Padding p, float f) => new((int)Math.Round(p.Left * f), (int)Math.Round(p.Top * f), (int)Math.Round(p.Right * f), (int)Math.Round(p.Bottom * f));
        control.Padding = S(control.Padding, scale); control.Margin = S(control.Margin, scale);
        if (control is not Form && control.MaximumSize != Size.Empty)
            control.MaximumSize = new Size((int)Math.Round(control.MaximumSize.Width * scale), (int)Math.Round(control.MaximumSize.Height * scale));
        if (control is TableLayoutPanel table)
        {
            foreach (RowStyle r in table.RowStyles) if (r.SizeType == SizeType.Absolute) r.Height *= scale;
            foreach (ColumnStyle c in table.ColumnStyles) if (c.SizeType == SizeType.Absolute) c.Width *= scale;
        }
        foreach (Control child in control.Controls) ScaleTree(child, scale);
        if (control is Form f)
        {
            f.ClientSize = new Size((int)Math.Round(f.ClientSize.Width * scale), (int)Math.Round(f.ClientSize.Height * scale));
            f.MinimumSize = new Size((int)Math.Round(f.MinimumSize.Width * scale), (int)Math.Round(f.MinimumSize.Height * scale));
        }
        else control.Size = new Size((int)Math.Round(control.Width * scale), (int)Math.Round(control.Height * scale));
        control.ResumeLayout(true);
    }
}
