using System.ComponentModel;
using System.Text.Json;

namespace ReadDock;

internal static class PopupPlacement
{
    public static Rectangle Calculate(Rectangle anchor, Size requested, Rectangle workingArea, int gap = 12)
    {
        int width = Math.Min(requested.Width, workingArea.Width), height = Math.Min(requested.Height, workingArea.Height);
        int x = Math.Clamp(anchor.Left, workingArea.Left, workingArea.Right - width);
        int below = anchor.Bottom + gap;
        int y = below + height <= workingArea.Bottom ? below : anchor.Top - gap - height;
        y = Math.Clamp(y, workingArea.Top, workingArea.Bottom - height);
        return new Rectangle(x, y, width, height);
    }
}

internal sealed class TranslationPopup : Form
{
    private readonly Label heading;
    private readonly Label status = Ui.Label("", false, 9);
    private readonly RichTextBox content = new();
    private readonly MathReadingView reading;
    private readonly Button toggle;
    private readonly Button collect;
    private readonly Action closeAction;
    private readonly TableLayoutPanel root;
    private bool fitting;
    private Rectangle? anchor;
    private string translatedText = "", originalText = "";
    private bool showingOriginal;
    private int? renderedHeight;
    private int revision;
    internal bool ShowingOriginal => showingOriginal;
    internal string SelectedText => reading.SelectedText.Length > 0 ? reading.SelectedText : content.SelectedText;
    internal Task<string> InspectReadingAsync(string script) => reading.InspectAsync(script);
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool PreserveOnClose { get; set; }
    public TranslationPopup(string notebook, Action openMain, Action collectWord, Action closeAction)
    {
        this.closeAction = closeAction;
        Text = "单词猫 · 轻量翻译"; Font = Ui.Font(); AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(600, 180); MinimumSize = new Size(380, 0); KeyPreview = true; Padding = new Padding(1);
        root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 14), ColumnCount = 1, RowCount = 4, Tag = "surface" };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Tag = "surface" };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
        heading = Ui.Label((UiText.Language == "en" ? "WordsCat  ·  " : "单词猫  ·  ") + notebook, true, 10);
        var close = Ui.Button("×", Close, false, 32); close.Height = 29;
        header.Controls.Add(heading, 0, 0); header.Controls.Add(close, 1, 0);
        heading.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) Native.DragWindow(Handle); };
        root.Controls.Add(header, 0, 0);
        content.Dock = DockStyle.Fill; content.BorderStyle = BorderStyle.None; content.ReadOnly = true; content.DetectUrls = false; content.Font = Ui.Font(12); content.Margin = new Padding(0, 8, 0, 4);
        content.AccessibleName = "轻量阅读";
        reading = new MathReadingView(content, false, true) { Margin = content.Margin };
        root.Controls.Add(reading, 0, 1);
        reading.Rendered += async () => await MeasureRenderedAsync();
        content.WordWrap = true; content.ScrollBars = RichTextBoxScrollBars.Vertical;
        root.Controls.Add(status, 0, 2);
        collect = Ui.Button("收藏单词", collectWord, false, 105);
        toggle = Ui.Button("显示原文", ToggleOriginal, false, 100);
        toggle.Name = "popupOriginalToggle";
        root.Controls.Add(Ui.Row(Ui.Button("打开主界面", openMain, true, 132), collect, toggle), 0, 3);
        Controls.Add(root); Ui.FinishLayout(this);
        FormClosed += (_, _) => { if (!PreserveOnClose) closeAction(); translatedText = originalText = ""; revision++; content.Clear(); };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { e.Handled = true; Close(); } };
        Shown += (_, _) => FitContent();
        DpiChanged += (_, _) => FitContent();
    }
    protected override bool ShowWithoutActivation => true;
    public void PlaceNear(Rectangle selection)
    {
        anchor = selection;
        Bounds = PopupPlacement.Calculate(selection, Size, Screen.FromRectangle(selection).WorkingArea, Math.Max(8, 8 * DeviceDpi / 96));
    }
    public void SetLoading(string message)
    {
        translatedText = originalText = ""; showingOriginal = false; toggle.Text = "显示原文";
        SetReadingText(UiText.T(message)); status.Text = "仅处理本次选区"; toggle.Enabled = collect.Enabled = false;
        FitContent();
    }
    public void SetResult(string text, string detail, string original = "")
    {
        translatedText = SecretRedactor.Redact(text); originalText = SecretRedactor.Redact(original);
        showingOriginal = false; toggle.Text = "显示原文"; SetReadingText(translatedText);
        status.Text = SecretRedactor.Redact(UiText.T(detail)); status.ForeColor = Ui.Muted;
        toggle.Enabled = originalText.Length > 0; collect.Enabled = translatedText.Length > 0;
        FitContent();
    }
    void ToggleOriginal()
    {
        if (originalText.Length == 0) return;
        showingOriginal = !showingOriginal;
        toggle.Text = showingOriginal ? "显示译文" : "显示原文";
        SetReadingText(showingOriginal ? originalText : translatedText);
        FitContent();
    }
    void SetReadingText(string text) { revision++; renderedHeight = null; content.Text = text; reading.RefreshContent(); }
    async Task MeasureRenderedAsync()
    {
        int current = revision;
        try
        {
            // Allow the bundled math font to settle before measuring the rendered document.
            await Task.Delay(120);
            if (IsDisposed || current != revision) return;
            string value = await reading.InspectAsync("Math.ceil(document.getElementById('content').getBoundingClientRect().height + 24)");
            if (IsDisposed || current != revision) return;
            using var doc = JsonDocument.Parse(value);
            if (doc.RootElement.TryGetInt32(out int height))
            { renderedHeight = Math.Max(32, height) * DeviceDpi / 96; FitContent(); }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ObjectDisposedException or System.Runtime.InteropServices.COMException) { }
    }
    public void UpdateStatus(string detail, bool error)
    {
        status.Text = SecretRedactor.Redact(detail); status.ForeColor = error ? Color.FromArgb(219, 132, 76) : Ui.Muted;
        FitContent();
    }
    public void SetError(string message)
    {
        translatedText = originalText = ""; showingOriginal = false; toggle.Text = "显示原文";
        SetReadingText(SecretRedactor.Redact(UiText.T(message))); status.Text = "可打开主界面修改原文或检查设置。"; toggle.Enabled = collect.Enabled = false;
        FitContent();
    }
    void FitContent()
    {
        if (fitting || IsDisposed) return;
        fitting = true;
        try
        {
            PerformLayout();
            float scale = DeviceDpi / 96f;
            int width = Math.Max(80, ClientSize.Width - root.Padding.Horizontal - content.Margin.Horizontal - (int)(20 * scale));
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding;
            int textHeight = renderedHeight ?? (TextRenderer.MeasureText(content.Text.Length == 0 ? " " : content.Text, content.Font, new Size(width, int.MaxValue), flags).Height + (int)(12 * scale));
            int statusHeight = Math.Clamp(TextRenderer.MeasureText(status.Text, status.Font, new Size(width, int.MaxValue), flags).Height + (int)(6 * scale), (int)(24 * scale), (int)(56 * scale));
            root.RowStyles[2].Height = statusHeight;
            int fixedHeight = root.Padding.Vertical + Padding.Vertical + (int)root.RowStyles[0].Height + (int)root.RowStyles[3].Height + statusHeight + content.Margin.Vertical;
            var area = anchor.HasValue ? Screen.FromRectangle(anchor.Value).WorkingArea : Screen.FromControl(this).WorkingArea;
            int maximum = Math.Min((int)(330 * scale), area.Height);
            ClientSize = new Size(ClientSize.Width, Math.Min(maximum, fixedHeight + Math.Max(textHeight, (int)(32 * scale))));
            if (anchor.HasValue) PlaceNear(anchor.Value);
        }
        finally { fitting = false; }
    }
    public void SetNotebook(string name) => heading.Text = (UiText.Language == "en" ? "WordsCat  ·  " : "单词猫  ·  ") + name;
    protected override void OnPaint(PaintEventArgs e) { using var pen = new Pen(Ui.Border); e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1); base.OnPaint(e); }
}
