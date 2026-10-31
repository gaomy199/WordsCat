using System.Text.RegularExpressions;

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

internal static class MathDisplay
{
    private const string NormalSub = "0123456789+-=()aehijklmnoprstuvx";
    private const string Subscripts = "₀₁₂₃₄₅₆₇₈₉₊₋₌₍₎ₐₑₕᵢⱼₖₗₘₙₒₚᵣₛₜᵤᵥₓ";
    public static string ForReading(string text) => Regex.Replace(text, @"(?<!\\)\$([^$\n]+)\$", m =>
    {
        string math = m.Groups[1].Value;
        math = Regex.Replace(math, @"\\(alpha|beta|gamma|delta|epsilon|theta|lambda|mu|nu|pi|rho|sigma|tau|phi|psi|omega|Delta|Sigma|Omega)(?![A-Za-z])", x =>
            new Dictionary<string, string> { ["alpha"] = "α", ["beta"] = "β", ["gamma"] = "γ", ["delta"] = "δ", ["epsilon"] = "ε", ["theta"] = "θ", ["lambda"] = "λ", ["mu"] = "μ", ["nu"] = "ν", ["pi"] = "π", ["rho"] = "ρ", ["sigma"] = "σ", ["tau"] = "τ", ["phi"] = "φ", ["psi"] = "ψ", ["omega"] = "ω", ["Delta"] = "Δ", ["Sigma"] = "Σ", ["Omega"] = "Ω" }[x.Groups[1].Value]);
        math = math.Replace(@"\ldots", "…").Replace(@"\cdots", "⋯").Replace(@"\times", "×").Replace(@"\leq", "≤").Replace(@"\geq", "≥");
        math = Regex.Replace(math, @"_(?:\{([0-9a-z+\-=()]+)\}|([0-9a-z]))", s =>
        {
            var value = s.Groups[1].Success ? s.Groups[1].Value : s.Groups[2].Value;
            return value.All(c => NormalSub.Contains(c)) ? new string(value.Select(c => Subscripts[NormalSub.IndexOf(c)]).ToArray()) : s.Value;
        });
        return math.Contains('\\') || math.Contains('{') || math.Contains('^') ? "$" + math + "$" : math;
    });
}

internal sealed class TranslationPopup : Form
{
    private readonly Label heading;
    private readonly Label status = Ui.Label("", false, 9);
    private readonly RichTextBox content = new();
    private readonly Button copy;
    private readonly Button collect;
    private readonly Action closeAction;
    private readonly TableLayoutPanel root;
    private bool fitting;
    private Rectangle? anchor;
    private string rawText = "";
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
        content.AccessibleName = "轻量译文"; root.Controls.Add(content, 0, 1);
        content.WordWrap = true; content.ScrollBars = RichTextBoxScrollBars.Vertical;
        root.Controls.Add(status, 0, 2);
        collect = Ui.Button("收藏单词", collectWord, false, 105);
        copy = Ui.Button("复制译文", () => { try { if (rawText.Length > 0) Clipboard.SetText(rawText); } catch (Exception ex) { Ui.Error(this, ex); } }, false, 100);
        root.Controls.Add(Ui.Row(Ui.Button("打开主界面", openMain, true, 132), collect, copy), 0, 3);
        Controls.Add(root); Ui.FinishLayout(this);
        FormClosed += (_, _) => { if (!PreserveOnClose) closeAction(); rawText = ""; content.Clear(); };
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
        rawText = ""; content.Text = UiText.T(message); status.Text = "仅处理本次选区"; copy.Enabled = collect.Enabled = false;
        FitContent();
    }
    public void SetResult(string text, string detail)
    {
        text = SecretRedactor.Redact(text); rawText = text; content.Text = MathDisplay.ForReading(text); status.Text = SecretRedactor.Redact(UiText.T(detail)); status.ForeColor = Ui.Muted; copy.Enabled = collect.Enabled = text.Length > 0;
        FitContent();
    }
    public void UpdateStatus(string detail, bool error)
    {
        status.Text = SecretRedactor.Redact(detail); status.ForeColor = error ? Color.FromArgb(219, 132, 76) : Ui.Muted;
        FitContent();
    }
    public void SetError(string message)
    {
        content.Text = SecretRedactor.Redact(UiText.T(message)); status.Text = "可打开主界面修改原文或检查设置。"; copy.Enabled = collect.Enabled = false;
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
            int textHeight = TextRenderer.MeasureText(content.Text.Length == 0 ? " " : content.Text, content.Font, new Size(width, int.MaxValue), flags).Height + (int)(12 * scale);
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
