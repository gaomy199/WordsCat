using System.Text;

namespace ReadDock;

internal sealed class MainWindow : Form
{
    private readonly AppConfig config;
    private readonly bool offlinePreview;
    private readonly ICredentialVault credentials;
    private readonly bool customVault;
    private ServiceSetupGuide? setupGuide;
    string ReadKey() => offlinePreview ? "" : credentials.Read(config.BaseUrl);
    private readonly IModelService api;
    private readonly VocabularyStore store = new();
    private readonly ReadingHistory history;
    private readonly ListBox historyList = new() { Dock = DockStyle.Fill, IntegralHeight = false, Font = Ui.Font(9), AccessibleName = "最近翻译记录" };
    private readonly Label historyHeading = Ui.Label("最近翻译", true, 11);
    private readonly TableLayoutPanel outer = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
    private TableLayoutPanel sidebar = null!;
    private Label brandTitle = null!;
    private Button sidebarToggle = null!;
    private readonly RichTextBox source = new();
    private readonly RichTextBox translation = new();
    private readonly MathReadingView sourceReading;
    private readonly MathReadingView translationReading;
    private readonly ComboBox notebookChoice = new SoftComboBox() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = Ui.Font() };
    private readonly ToolTip tips = new();
    private readonly TextBox question = Ui.TextBox();
    private readonly CheckBox cleanLines = new() { Text = "合并 PDF 断行", Checked = true, AutoSize = true, Font = Ui.Font(), ForeColor = Ui.Muted };
    private readonly CheckBox pin = new() { Text = "窗口置顶", AutoSize = true, Font = Ui.Font(), ForeColor = Ui.Muted };
    private readonly Label directionLabel = Ui.Label("自动识别 · 中英互译");
    private readonly Label sourceHeading = Ui.Label("", true, 12);
    private readonly Label translationHeading = Ui.Label("", true, 12);
    private readonly Label status = Ui.Label("就绪", false, 10);
    private readonly Label modelBadge = Ui.Label("", false, 10);
    private readonly List<Button> workButtons = [];
    private readonly NotifyIcon tray;
    private readonly FloatingDock floating;
    private readonly ContextMenuStrip menu;
    private readonly System.Windows.Forms.Timer foregroundTracker = new() { Interval = 200 };
    private readonly List<ChatMessage> currentConversation = [];
    private readonly Button undoButton;
    private Icon dockIcon;
    private TranslationPopup? popup;
    private CancellationTokenSource? cancellation;
    private IntPtr lastSource;
    private bool quitting, busy, dialogOpen;
    private bool clearWhenIdle;
    private string sessionOriginal = "";
    private string sessionTranslation = "";
    private string sessionDirection = "";
    private readonly List<string> hotkeyFailures = [];

    public MainWindow(AppConfig config, bool previewMode, bool offlinePreview = false, IModelService? modelService = null, ICredentialVault? credentialVault = null)
    {
        api = modelService ?? new OpenAiCompatibleClient();
        customVault = credentialVault != null;
        credentials = credentialVault ?? (offlinePreview ? new DisabledCredentialVault() : new WindowsCredentialVault());
        this.config = config;
        this.offlinePreview = offlinePreview;
        config.NormalizeNotebooks();
        UiText.Language = config.UiLanguage;
        history = new ReadingHistory(AppPaths.DataDirectory);
        try { history.Trim(config.HistoryLimit); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        Text = "单词猫 WordsCat · 中英阅读助手"; BackColor = Ui.Paper; ForeColor = Ui.Ink; Font = Ui.Font();
        AutoScaleMode = AutoScaleMode.None; StartPosition = FormStartPosition.CenterScreen; KeyPreview = true;
        ClientSize = new Size(1380, 780); MinimumSize = new Size(1140, 680); ShowInTaskbar = true;
        dockIcon = Ui.CreateAppIcon(); Icon = dockIcon;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 16), ColumnCount = 1, RowCount = 7 };
        foreach (var height in new[] { 80, 46, 46 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 46, 46, 36 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); header.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        var workspaceTitle = Ui.Label("中英互译", true, 20); workspaceTitle.TextAlign = ContentAlignment.MiddleLeft;
        header.Controls.Add(workspaceTitle, 0, 0);
        header.Controls.Add(Ui.Label("在阅读中理解，在语境中积累。", false, 10), 0, 1);
        modelBadge.Dock = DockStyle.Fill; modelBadge.TextAlign = ContentAlignment.MiddleRight; header.Controls.Add(modelBadge, 1, 0);
        var mode = Ui.Label("中英互译 · 手动收藏 · 本地历史"); mode.Dock = DockStyle.Fill; mode.TextAlign = ContentAlignment.MiddleRight; header.Controls.Add(mode, 1, 1);
        root.Controls.Add(header, 0, 0);

        var selectionButton = WorkButton("划词翻译", async () => await CaptureTextAsync());
        var screenshotButton = WorkButton("截图翻译", async () => await CaptureImageAsync());
        var pasteButton = WorkButton("粘贴原文", Paste);
        var wordButton = WorkButton("收藏所选词", SaveWord, true, 135);
        var settingsButton = WorkButton("设置", Settings, false, 104);
        undoButton = Ui.Button("撤销收藏", Undo, false, 105); undoButton.Enabled = false;
        root.Controls.Add(Ui.Row(selectionButton, screenshotButton, pasteButton, wordButton, Ui.Button("打开单词本", OpenVocabulary, false, 120), settingsButton, Ui.Button("退出", Quit, false, 70)), 0, 1);

        var metadata = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Margin = Padding.Empty };
        metadata.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72)); metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
        metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 204)); metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        var saveTo = Ui.Label("保存到"); saveTo.TextAlign = ContentAlignment.MiddleLeft;
        saveTo.Margin = new Padding(0, 3, 0, 3); notebookChoice.Margin = new Padding(0, 3, 8, 3);
        pin.Anchor = AnchorStyles.Left; pin.Margin = new Padding(0, 3, 8, 3);
        var notebookFrame = new FieldFrame(notebookChoice) { Margin = new Padding(0, 2, 10, 4) };
        var directionFrame = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 8, 4), Padding = new Padding(10, 0, 10, 0), Radius = 8 };
        directionLabel.Margin = Padding.Empty; directionLabel.TextAlign = ContentAlignment.MiddleLeft; directionLabel.AccessibleName = "translationDirection";
        directionFrame.Controls.Add(directionLabel);
        metadata.Controls.Add(saveTo, 0, 0); metadata.Controls.Add(notebookFrame, 1, 0); metadata.Controls.Add(pin, 2, 0);
        metadata.Controls.Add(directionFrame, 3, 0);
        var swapButton = Ui.CircleButton("交换", SwapDirection); workButtons.Add(swapButton);
        tips.SetToolTip(swapButton, UiText.T("交换"));
        swapButton.Anchor = AnchorStyles.None; swapButton.Margin = new Padding(0, 2, 0, 4); metadata.Controls.Add(swapButton, 4, 0);
        notebookChoice.SelectedIndexChanged += (_, _) => { if (notebookChoice.SelectedItem is NotebookTarget n) { config.ActiveNotebookId = n.Id; tips.SetToolTip(notebookChoice, n.File); popup?.SetNotebook(n.Name); } };
        pin.CheckedChanged += (_, _) => TopMost = pin.Checked; root.Controls.Add(metadata, 0, 2);

        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16)); split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        ConfigureEditor(source, false); ConfigureEditor(translation, true);
        sourceReading = new MathReadingView(source, true); translationReading = new MathReadingView(translation, false);
        source.TextChanged += (_, _) => SourceChanged();
        source.AccessibleName = "原文"; translation.AccessibleName = "译文";
        split.Controls.Add(EditorCard(sourceHeading, "阅读公式 · 编辑原文 · 查看原始截图；可选词收藏。", sourceReading), 0, 0);
        split.Controls.Add(EditorCard(translationHeading, "保留段落与独立公式；追问限于当前选区。", translationReading), 2, 0);
        root.Controls.Add(split, 0, 3);
        var translateButton = WorkButton("翻译当前原文", async () => await TranslateAsync(), true, 140);
        root.Controls.Add(Ui.Row(translateButton, cleanLines, Ui.Button("清空本次内容", () => { if (busy) { cancellation?.Cancel(); return; } ClearSession(); }), undoButton, Ui.Button("取消请求", () => cancellation?.Cancel(), false, 100)), 0, 4);

        var follow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        follow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); follow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        follow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        question.PlaceholderText = UiText.T("输入语法、句意或追问当前选区其他问题");
        question.Multiline = true; question.AcceptsReturn = false; question.Margin = new Padding(0, 4, 8, 6); question.AccessibleName = "followUpQuestion";
        var questionFrame = new FieldFrame(question) { Margin = new Padding(0, 4, 8, 6), AccessibleName = "followUpFrame" };
        follow.Controls.Add(questionFrame, 0, 0);
        var askButton = WorkButton("追问", async () => await FollowUpAsync(), false, 108);
        askButton.Dock = DockStyle.Fill; askButton.Margin = new Padding(0, 4, 0, 6); askButton.AccessibleName = "followUpSubmit";
        follow.Controls.Add(askButton, 1, 0); root.Controls.Add(follow, 0, 5);
        question.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await FollowUpAsync(); } };
        status.Dock = DockStyle.Fill; status.Padding = new Padding(0, 3, 0, 0); root.Controls.Add(status, 0, 6);
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260)); outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outer.Controls.Add(HistoryPanel(), 0, 0); outer.Controls.Add(root, 1, 0); Controls.Add(outer);

        menu = new ContextMenuStrip { Font = Ui.Font(10), ShowImageMargin = false };
        AddMenu("划词翻译          Ctrl+Alt+D", async () => await CaptureTextAsync());
        AddMenu("截图翻译          Ctrl+Alt+S", async () => await CaptureImageAsync());
        AddMenu("收藏单词          Ctrl+Alt+W", SaveWord);
        AddMenu("手动修改 / 粘贴原文", ShowMain);
        menu.Items.Add(new ToolStripSeparator()); AddMenu("打开单词本", OpenVocabulary); AddMenu("撤销最近收藏", Undo);
        AddMenu("设置", Settings); AddMenu("显示 / 隐藏主窗口", ToggleMain);
        AddMenu("显示 / 隐藏悬浮图标", () => { config.ShowFloating = !floating!.Visible; if (config.ShowFloating) floating.Show(); else floating.Hide(); TrySaveConfig(); });
        menu.Items.Add(new ToolStripSeparator()); AddMenu("退出单词猫", Quit);
        floating = new FloatingDock(menu, config);
        UiText.Apply(floating);
        floating.PositionSaved += p => { config.FloatingX = p.X; config.FloatingY = p.Y; TrySaveConfig(); };
        tray = new NotifyIcon { Icon = dockIcon, Text = UiText.T("单词猫 WordsCat · Ctrl+Alt+S 截图翻译"), Visible = true, ContextMenuStrip = menu };
        Ui.ApplyTheme(menu);
        UiText.ApplyMenu(menu);
        tray.DoubleClick += (_, _) => ShowMain();
        foregroundTracker.Tick += (_, _) => { var h = Native.GetForegroundWindow(); if (h != IntPtr.Zero && !Native.IsOurWindow(h)) lastSource = h; }; foregroundTracker.Start();
        Shown += (_, _) =>
        {
            // RichTextBox does not raise native text notifications before its hidden
            // edit tab has a handle. Create it once so paste/capture updates headings too.
            _ = source.Handle;
            if (config.ShowFloating) floating.Show();
            bool needsGuide = false;
            if (!config.ServiceGuideSeen)
                try { needsGuide = ServiceSetupGuide.ShouldShow(config, credentials.HasKey(config.BaseUrl)); }
                catch { SetStatus("无法检查已保存密钥，请在设置中检查模型服务。", true); }
            if (!config.ShowOnStartup && !previewMode && !needsGuide) Hide();
            if (hotkeyFailures.Count > 0) SetStatus(UiText.F("快捷键被其他软件占用：{0}。可使用悬浮菜单。", string.Join(", ", hotkeyFailures)), true);
            if (history.LoadError != null) SetStatus(history.LoadError, true);
            if (previewMode)
            {
                source.Text = "Let us put $\\theta=q$. The Hamiltonian function $H$ is given by\n\n$$H=T+V=\\frac{m}{2}(l\\dot{q})^2+mgl(1-\\cos q)$$\n\nSince $p$ is defined by\n\n$$p=\\frac{\\partial T}{\\partial\\dot q}=ml^2\\dot q$$";
                translation.Text = "令 $\\theta=q$。哈密顿函数 $H$ 为\n\n$$H=T+V=\\frac{m}{2}(l\\dot{q})^2+mgl(1-\\cos q)$$\n\n由于 $p$ 定义为\n\n$$p=\\frac{\\partial T}{\\partial\\dot q}=ml^2\\dot q$$";
                SetStatus("界面示例 · 当前译文为内置示例，未调用 API。可在设置中配置后开始使用。");
            }
            if (needsGuide)
            {
                setupGuide = new ServiceSetupGuide(Settings, () => { config.ServiceGuideSeen = true; TrySaveConfig(); });
                setupGuide.Show(this);
            }
        };
        KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.Q && !dialogOpen) { e.Handled = true; Quit(); } };
        FormClosing += (_, e) => { if (!quitting) { e.Cancel = true; HideMain(); } };
        UpdateBadge();
        RefreshNotebooks();
        RefreshHistory();
        UpdateDirectionLabels();
        Ui.FinishLayout(this);
        ApplySidebarState();
    }
    Control HistoryPanel()
    {
        var panel = sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18, 12, 8, 20), ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 56)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        var brandRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        brandRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); brandRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        brandTitle = Ui.Label("单词猫", true, 21); brandTitle.Name = "brandTitle"; brandTitle.Margin = Padding.Empty; brandTitle.TextAlign = ContentAlignment.MiddleLeft;
        sidebarToggle = Ui.CircleButton("收起侧边栏", () => { config.SidebarCollapsed = !config.SidebarCollapsed; ApplySidebarState(); TrySaveConfig(); }, 32);
        sidebarToggle.Name = "sidebarToggle"; sidebarToggle.Margin = Padding.Empty; sidebarToggle.Anchor = AnchorStyles.None;
        brandRow.Controls.Add(brandTitle, 0, 0); brandRow.Controls.Add(sidebarToggle, 1, 0);
        panel.Controls.Add(brandRow, 0, 0);
        historyList.BorderStyle = BorderStyle.None;
        panel.Controls.Add(historyHeading, 0, 1); panel.Controls.Add(Ui.Card(historyList), 0, 2);
        historyList.DrawMode = DrawMode.OwnerDrawFixed; historyList.ItemHeight = Math.Max(44, 48 * DeviceDpi / 96);
        historyList.DrawItem += (_, e) =>
        {
            if (e.Index < 0 || historyList.Items[e.Index] is not ReadingRecord item) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using var background = new SolidBrush(Ui.Surface); e.Graphics.FillRectangle(background, e.Bounds);
            var itemBounds = Rectangle.Inflate(e.Bounds, -2, -3); e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var shape = Ui.RoundPath(itemBounds, 8 * DeviceDpi / 96f);
            using var fill = new SolidBrush(selected ? Ui.Blend(Ui.Surface, Ui.Teal, Ui.IsDark ? .24f : .12f) : Ui.Surface); e.Graphics.FillPath(fill, shape);
            var rect = Rectangle.Inflate(e.Bounds, -10, -5); rect.Height /= 2;
            TextRenderer.DrawText(e.Graphics, item.CreatedAt.ToLocalTime().ToString("MM-dd HH:mm:ss"), historyList.Font, rect, Ui.Muted);
            rect.Y += rect.Height;
            TextRenderer.DrawText(e.Graphics, System.Text.RegularExpressions.Regex.Replace(item.Original, @"\s+", " "), historyList.Font, rect, Ui.Ink, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        };
        historyList.DoubleClick += (_, _) => RestoreHistory();
        var actions = Ui.Row(Ui.Button("打开记录", RestoreHistory, false, 104), Ui.Button("清空历史", ClearHistory, false, 104)); actions.Padding = new Padding(0, 10, 0, 0);
        panel.Controls.Add(actions, 0, 3);
        return panel;
    }
    void ApplySidebarState()
    {
        bool collapsed = config.SidebarCollapsed;
        float scale = DeviceDpi / 96f;
        outer.SuspendLayout(); sidebar.SuspendLayout();
        outer.ColumnStyles[0].Width = (collapsed ? 60 : 260) * scale;
        sidebar.Padding = new Padding((int)((collapsed ? 12 : 18) * scale), (int)(12 * scale), (int)(8 * scale), (int)(20 * scale));
        brandTitle.Visible = !collapsed;
        for (int i = 1; i < sidebar.Controls.Count; i++) sidebar.Controls[i].Visible = !collapsed;
        string caption = collapsed ? "展开侧边栏" : "收起侧边栏";
        sidebarToggle.Text = UiText.T(caption); sidebarToggle.AccessibleName = UiText.T(caption);
        Ui.SetButtonGlyph(sidebarToggle, UiGlyph.For(caption)); tips.SetToolTip(sidebarToggle, UiText.T(caption));
        sidebar.ResumeLayout(true); outer.ResumeLayout(true);
    }
    void RefreshHistory()
    {
        historyList.BeginUpdate(); historyList.Items.Clear();
        foreach (var record in history.Records) historyList.Items.Add(record);
        historyList.EndUpdate();
        historyHeading.Text = UiText.F("最近翻译  {0} / {1}", history.Records.Count, config.HistoryLimit);
    }
    void RetainTranslation(string original, string translated)
    {
        try { history.Add(original, translated, config.HistoryLimit, CurrentDirection); RefreshHistory(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        { SetStatus(UiText.T("翻译已完成，但本地历史保存失败：") + ex.Message, true); }
    }
    void RestoreHistory()
    {
        if (busy || dialogOpen || historyList.SelectedItem is not ReadingRecord record) return;
        ClearSession();
        source.Text = record.Original; translation.Text = record.Translation;
        CompleteSession(record.Original, record.Translation, record.Direction);
        currentConversation.AddRange(Prompts.Translation(config, record.Original)); currentConversation.Add(new("assistant", record.Translation));
        ShowMain(); SetStatus("已打开本地历史 · 原始截图不保留。");
    }
    void ClearHistory()
    {
        if (busy || dialogOpen) return;
        if (UiText.Show(this, UiText.F("永久删除全部 {0} 条本地历史？删除后软件内无法恢复，已收藏单词不受影响。", history.Records.Count), "清空历史", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        try { history.Clear(); RefreshHistory(); } catch (Exception ex) { Ui.Error(this, ex); }
    }
    static void ConfigureEditor(RichTextBox r, bool readOnly)
    {
        r.Dock = DockStyle.Fill; r.BorderStyle = BorderStyle.None; r.Font = Ui.Font(12);
        r.BackColor = Ui.Surface; r.ForeColor = Ui.Ink; r.ReadOnly = readOnly; r.DetectUrls = false;
        r.HideSelection = false; r.ScrollBars = RichTextBoxScrollBars.Vertical; r.Margin = new Padding(0, 8, 0, 0);
    }
    static Control EditorCard(Label title, string subtitle, Control editor)
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Ui.Surface, Tag = "surface", Padding = new Padding(18, 12, 18, 16), ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, CellBorderStyle = TableLayoutPanelCellBorderStyle.None };
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); p.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        p.Padding = new Padding(10, 4, 10, 6);
        p.Controls.Add(title, 0, 0); p.Controls.Add(Ui.Label(subtitle), 0, 1); p.Controls.Add(editor, 0, 2); return Ui.Card(p);
    }
    Button WorkButton(string text, Action action, bool primary = false, int width = 115) { var b = Ui.Button(text, action, primary, width); workButtons.Add(b); return b; }
    string CurrentDirection => sessionOriginal.Length > 0 && source.Text == sessionOriginal && sessionDirection.Length > 0 ? sessionDirection : TranslationLanguage.DetectDirection(source.Text);
    void UpdateDirectionLabels()
    {
        bool empty = string.IsNullOrWhiteSpace(source.Text), toEnglish = CurrentDirection == "zh-en";
        sourceHeading.Text = UiText.T(empty ? "01   原文" : toEnglish ? "01   中文原文" : "01   英文原文");
        translationHeading.Text = UiText.T(empty ? "02   译文" : toEnglish ? "02   英文译文" : "02   中文译文");
        directionLabel.Text = UiText.T(empty ? "自动识别 · 中英互译" : toEnglish ? "自动 · 中文 → 英文" : "自动 · 英文 → 中文");
    }
    void ClearTranslation()
    {
        currentConversation.Clear(); translation.Clear(); question.Clear(); sessionOriginal = ""; sessionTranslation = ""; sessionDirection = "";
    }
    void SourceChanged()
    {
        if (!busy && (sessionOriginal.Length > 0 && source.Text != sessionOriginal || sessionOriginal.Length == 0 && translation.TextLength > 0)) ClearTranslation();
        UpdateDirectionLabels();
    }
    void CompleteSession(string original, string translated, string? direction = null)
    {
        sessionOriginal = original; sessionTranslation = translated;
        sessionDirection = direction is "en-zh" or "zh-en" ? direction : TranslationLanguage.DetectDirection(original);
        UpdateDirectionLabels();
    }
    void SwapDirection()
    {
        if (busy || dialogOpen) return;
        string nextSource = sessionTranslation.Length > 0 ? sessionTranslation : translation.Text;
        if (string.IsNullOrWhiteSpace(nextSource)) { SetStatus("请先完成翻译，再交换原文与译文。"); return; }
        DismissPopup(true); ClearTranslation();
        source.Text = nextSource; sourceReading.ClearTransientContent();
        SetStatus("译文已放入左侧，已自动识别语言，可重新翻译。");
    }
    void AddMenu(string text, Action action) => menu.Items.Add(text, null, (_, _) => { if (!busy && !dialogOpen) action(); else SetStatus("请先完成或取消当前操作。"); });
    void UpdateBadge() => modelBadge.Text = (config.ModelProvider == "custom" ? UiText.T("自定义 OpenAI 兼容") : "DeepSeek") + "  ·  " + config.Model + "  ·  v0.3.7";
    void SetStatus(string text, bool error = false) { text = SecretRedactor.Redact(UiText.T(text)); status.Text = text; status.ForeColor = error ? Color.FromArgb(196, 110, 56) : Ui.Muted; if (popup is { IsDisposed: false }) popup.UpdateStatus(text, error); }
    void SetBusy(bool value)
    {
        busy = value; foreach (var b in workButtons) b.Enabled = !value;
        source.ReadOnly = value; question.ReadOnly = value; cleanLines.Enabled = !value; notebookChoice.Enabled = !value;
    }
    async Task RunAsync(string message, Func<CancellationToken, Task> work)
    {
        if (busy || dialogOpen) return;
        cancellation = new CancellationTokenSource(); var token = cancellation.Token; SetBusy(true); SetStatus(message);
        try { await work(token); }
        catch (OperationCanceledException) { SetStatus("已取消请求。"); if (popup is { IsDisposed: false }) popup.SetError("已取消本次请求。"); }
        catch (Exception ex) { SetStatus(ex.Message, true); if (popup is { IsDisposed: false }) popup.SetError(ex.Message); }
        finally { cancellation.Dispose(); cancellation = null; if (!IsDisposed) { SetBusy(false); if (clearWhenIdle) { clearWhenIdle = false; ClearSession(); } } }
    }
    public void ShowMain()
    {
        if (IsDisposed || quitting) return;
        // Finish the popup's click/close before Windows transfers activation to this window.
        DismissPopup(true);
        Show(); WindowState = FormWindowState.Normal; BringToFront(); Activate();
        BeginInvoke((Action)(() => { if (!IsDisposed && Visible && !quitting) { BringToFront(); Activate(); Native.SetForegroundWindow(Handle); } }));
    }
    void HideMain() { cancellation?.Cancel(); DismissPopup(true); Hide(); ClearSession(); if (busy) clearWhenIdle = true; }
    void ToggleMain() { if (Visible && WindowState != FormWindowState.Minimized) HideMain(); else ShowMain(); }
    void ClearSession()
    {
        ClearTranslation(); source.Clear();
        sourceReading.ClearTransientContent(); translationReading.ClearTransientContent();
        SetStatus("本次翻译和追问已清空。已收藏的单词会保留。");
    }
    void DismissPopup(bool preserve)
    {
        var old = popup; popup = null;
        if (old == null || old.IsDisposed) return;
        old.PreserveOnClose = preserve; old.Close(); old.Dispose();
    }
    internal void OpenPopup(Rectangle anchor, string progress)
    {
        DismissPopup(true);
        TranslationPopup? created = null;
        created = new TranslationPopup(config.ActiveNotebook.Name, ShowMain, () => OpenWordDialog(created!, false), () =>
        {
            if (ReferenceEquals(popup, created)) popup = null;
            cancellation?.Cancel();
            ClearSession(); if (busy) clearWhenIdle = true;
        });
        popup = created; popup.SetLoading(progress); popup.PlaceNear(anchor); popup.Show();
    }
    void RefreshNotebooks()
    {
        config.NormalizeNotebooks();
        notebookChoice.BeginUpdate(); notebookChoice.Items.Clear();
        foreach (var n in config.Notebooks) notebookChoice.Items.Add(n);
        notebookChoice.SelectedItem = config.ActiveNotebook; notebookChoice.EndUpdate();
        popup?.SetNotebook(config.ActiveNotebook.Name);
    }
    void Paste()
    {
        try
        {
            if (Clipboard.ContainsText()) { string pasted = Clipboard.GetText(); ClearSession(); source.Text = pasted; ShowMain(); SetStatus("已粘贴原文，核对后点击「翻译当前原文」。"); }
            else SetStatus("剪贴板里没有文字，可直接在左侧输入原文。", true);
        }
        catch (Exception ex) { Ui.Error(this, ex); }
    }
    async Task CaptureTextAsync()
    {
        if (busy || dialogOpen) return;
        var h = Native.GetForegroundWindow(); if (h != IntPtr.Zero && !Native.IsOurWindow(h)) lastSource = h;
        await RunAsync("正在获取阅读器中选中的文字…", async token =>
        {
            menu.Close(); DismissPopup(true); await Task.Delay(120, token);
            var text = await Native.CopySelectionAsync(lastSource); token.ThrowIfCancellationRequested();
            ClearSession(); source.Text = text; Hide();
            OpenPopup(new Rectangle(Cursor.Position, new Size(1, 1)), "正在翻译所选文字…");
            if (string.IsNullOrWhiteSpace(ReadKey())) { SetStatus("未配置 API Key，可打开主界面进行设置。"); popup?.SetResult(text, "已取得原文 · 未配置 API Key，尚未翻译"); }
            else await TranslateCoreAsync(token);
        });
    }
    async Task CaptureImageAsync()
    {
        if (busy || dialogOpen) return;
        await RunAsync("请框选要翻译的文字区域…", async token =>
        {
            bool wasVisible = Visible, floatVisible = floating.Visible;
            menu.Close(); DismissPopup(true); floating.Hide(); Hide(); await Task.Delay(200, token);
            Bitmap? image = null;
            Rectangle anchor = new(Cursor.Position, new Size(1, 1));
            try { using var overlay = new CaptureOverlay(); if (overlay.ShowDialog() == DialogResult.OK) { image = overlay.SelectedImage; anchor = overlay.SelectedScreenBounds; } }
            finally { if (floatVisible) floating.Show(); }
            if (image == null) { if (wasVisible) ShowMain(); SetStatus("已取消截图。"); return; }
            try
            {
                ClearSession(); sourceReading.SetCapture(image); OpenPopup(anchor, config.ScreenshotEngine == "vision" ? "正在识别数学符号并翻译…" : "正在使用本地 OCR 识别…");
                var key = ReadKey();
                if (config.ScreenshotEngine == "vision" && !string.IsNullOrWhiteSpace(key))
                {
                    using var png = new MemoryStream(); image.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                    SetStatus("DeepSeek 视觉识别 · 正在发送本次框选图片…");
                    var r = await api.TranslateImageAsync(config, key, png.ToArray(), token); token.ThrowIfCancellationRequested();
                    source.Text = r.Original; translation.Text = r.Translation; CompleteSession(r.Original, r.Translation);
                    currentConversation.AddRange(Prompts.Translation(config, r.Original)); currentConversation.Add(new("assistant", r.Translation));
                    string detail = UiText.F("视觉识别完成 · 输入 {0} / 输出 {1} tokens", r.InputTokens, r.OutputTokens) + (r.Uncertainty.Length > 0 ? UiText.T(" · 待核对：") + r.Uncertainty : "");
                    SetStatus(detail); popup?.SetResult(r.Translation, detail, r.Original);
                    RetainTranslation(r.Original, r.Translation);
                }
                else
                {
                    var temp = Path.Combine(AppPaths.DataDirectory, "capture-" + Guid.NewGuid().ToString("N") + ".png");
                    string text;
                    try { using var prepared = OcrService.PrepareLocal(image); prepared.Save(temp, System.Drawing.Imaging.ImageFormat.Png); text = await OcrService.RunAsync(temp, token); }
                    finally { try { File.Delete(temp); } catch { } }
                    token.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("没有识别到文字。请放大 PDF 后重新框选，尽量只选一栏正文。");
                    source.Text = text;
                    if (string.IsNullOrWhiteSpace(key)) { SetStatus("已完成本地识别。未配置 Key，数学符号建议配置视觉模式后重试。"); popup?.SetResult(text, "本地 OCR 原文 · 尚未翻译，可打开主界面校正"); }
                    else await TranslateCoreAsync(token);
                }
            }
            finally { image.Dispose(); }
        });
    }
    async Task TranslateAsync() => await RunAsync("正在翻译当前原文…", TranslateCoreAsync);
    async Task TranslateCoreAsync(CancellationToken token)
    {
        var text = cleanLines.Checked ? Prompts.CleanPdfText(source.Text) : source.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("请先选取、粘贴或输入原文。");
        if (text.Length > 16000) throw new InvalidOperationException("本工具适合局部阅读，请将本次选区控制在 16000 个字符以内。");
        ClearTranslation();
        var messages = Prompts.Translation(config, text);
        SetStatus("正在请求 DeepSeek，仅发送当前原文和阅读术语表…");
        var r = await api.CompleteAsync(config, ReadKey(), messages, false, token); token.ThrowIfCancellationRequested();
        source.Text = text; translation.Text = r.Text; CompleteSession(text, r.Text);
        currentConversation.AddRange(messages); currentConversation.Add(new("assistant", r.Text));
        SetStatus(UiText.F("翻译完成 · 输入 {0} / 输出 {1} tokens · 可在左侧选词收藏。", r.InputTokens, r.OutputTokens));
        popup?.SetResult(r.Text, UiText.F("翻译完成 · 输入 {0} / 输出 {1} tokens", r.InputTokens, r.OutputTokens), text);
        RetainTranslation(text, r.Text);
    }
    async Task FollowUpAsync()
    {
        await RunAsync("正在解释当前选区…", async token =>
        {
            if (sessionOriginal.Length == 0 || source.Text != sessionOriginal) throw new InvalidOperationException("请先翻译当前原文，再继续追问。");
            var q = question.Text.Trim(); if (q.Length == 0) throw new InvalidOperationException("请输入想追问的问题。");
            if (q.Length > 4000) throw new InvalidOperationException("追问请控制在 4000 个字符以内。");
            if (currentConversation.Count >= 11) throw new InvalidOperationException("本段已追问多次。请重新翻译当前原文，再开始新一轮追问。");
            var messages = currentConversation.ToList();
            messages[0] = new("system", Prompts.FollowUp(config, sessionOriginal, sessionDirection));
            messages.Add(new("user", q));
            var r = await api.CompleteAsync(config, ReadKey(), messages, false, token); token.ThrowIfCancellationRequested();
            currentConversation.Add(new("user", q)); currentConversation.Add(new("assistant", r.Text));
            translation.Text += "\n\n────────────\n" + UiText.T("追问：") + q + "\n\n" + r.Text; question.Clear(); translation.SelectionStart = translation.TextLength; translation.ScrollToCaret();
            SetStatus(UiText.F("已解释当前选区 · 输入 {0} / 输出 {1} tokens · 关闭窗口即清除本次上下文。", r.InputTokens, r.OutputTokens));
        });
    }
    void SaveWord() => OpenWordDialog(popup is { IsDisposed: false } ? popup : this, popup == null);
    void OpenWordDialog(IWin32Window owner, bool showMain)
    {
        if (busy || dialogOpen) return;
        var popupOwner = owner as TranslationPopup;
        bool restorePopup = popupOwner is { Visible: true };
        try
        {
            string word = (popupOwner != null ? popupOwner.SelectedText : sourceReading.SelectedText.Length > 0 ? sourceReading.SelectedText : source.SelectedText).Trim();
            if (word.Length == 0 && source.Text.Trim().Length <= 80 && !source.Text.Contains('\n')) word = source.Text.Trim();
            if (word.Length > 160) { SetStatus("请选择一个单词或短语，最多 160 个字符。", true); return; }
            if (showMain) ShowMain(); dialogOpen = true;
            using var dialog = new WordDialog(config, store, api, word, source.Text, ReadKey);
            // A topmost capture popup must not cover its modal editor. Preserve the
            // result while editing, then restore it without opening the main window.
            dialog.TopMost = (owner as Form)?.TopMost == true;
            if (restorePopup) popupOwner!.Hide();
            dialog.ShowDialog(owner);
            if (dialog.Saved) { RefreshNotebooks(); undoButton.Enabled = true; SetStatus(UiText.T("已收藏到：") + config.ActiveNotebook.File); }
        }
        catch (Exception ex) { Ui.Error(this, ex); }
        finally
        {
            dialogOpen = false;
            if (restorePopup && !quitting && popupOwner is { IsDisposed: false } && ReferenceEquals(popup, popupOwner)) popupOwner.Show();
        }
    }
    void Settings()
    {
        if (busy || dialogOpen) return;
        if (setupGuide is { IsDisposed: false }) setupGuide.Close();
        try
        {
            ShowMain(); dialogOpen = true;
            int oldHistoryLimit = config.HistoryLimit;
            using var dialog = new SettingsDialog(config, RefreshAppearance, !offlinePreview, history, applyLanguage: RefreshLanguage, credentialVault: offlinePreview && !customVault ? null : credentials); dialog.ShowDialog(this);
            RefreshLanguage(config.UiLanguage); SetStatus("就绪");
            if (config.HistoryLimit < oldHistoryLimit) ClearSession();
            RefreshAppearance(config);
            if (config.ShowFloating) floating.Show(); else floating.Hide(); UpdateBadge(); RefreshNotebooks();
            RefreshHistory();
        }
        catch (Exception ex) { Ui.Error(this, ex); }
        finally { dialogOpen = false; }
    }
    void RefreshLanguage(string language)
    {
        UiText.Language = language; UiText.Apply(this); UiText.ApplyMenu(menu);
        sourceReading.RefreshLanguage(); translationReading.RefreshLanguage(); UiText.Apply(floating);
        tray.Text = UiText.T("单词猫 WordsCat · Ctrl+Alt+S 截图翻译");
        question.PlaceholderText = UiText.T("输入语法、句意或追问当前选区其他问题");
        UpdateDirectionLabels(); RefreshHistory(); UpdateBadge();
        ApplySidebarState();
    }
    void RefreshAppearance(AppConfig appearance)
    {
        Ui.SetAppearance(appearance); Ui.ApplyTheme(this); Ui.ApplyTheme(menu); floating.RefreshAppearance();
        sourceReading.RefreshAppearance(); translationReading.RefreshAppearance();
        if (popup is { IsDisposed: false }) Ui.ApplyTheme(popup);
        var oldIcon = dockIcon; dockIcon = Ui.CreateAppIcon(); Icon = dockIcon; tray.Icon = dockIcon; oldIcon.Dispose();
    }
    void OpenVocabulary() { try { Ui.OpenFile(config.ActiveNotebook.File); } catch (Exception ex) { ShowMain(); Ui.Error(this, ex); } }
    void Undo()
    {
        if (busy || dialogOpen) return;
        try
        {
            if (store.LastSave == null) { SetStatus("本次运行还没有可撤销的收藏。"); return; }
            ShowMain();
            if (UiText.Show(this, UiText.F("撤销最近一次收藏？\n目标：{0}\n之后手动修改过的笔记不会被自动回退。", store.LastSave.Path), "撤销收藏", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            store.Undo(); undoButton.Enabled = false; SetStatus("已撤销最近一次收藏。");
        }
        catch (Exception ex) { Ui.Error(this, ex); }
    }
    void TrySaveConfig() { try { config.Save(); } catch (Exception ex) { SetStatus(UiText.T("设置未保存：") + ex.Message, true); } }
    void Quit() { cancellation?.Cancel(); DismissPopup(true); quitting = true; config.FloatingX = floating.Left; config.FloatingY = floating.Top; TrySaveConfig(); tray.Visible = false; floating.Close(); Close(); }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        foreach (var (id, key, label) in new[] { (1, Keys.D, "Ctrl+Alt+D"), (2, Keys.S, "Ctrl+Alt+S"), (3, Keys.W, "Ctrl+Alt+W"), (4, Keys.R, "Ctrl+Alt+R") })
            if (!Native.RegisterHotKey(Handle, id, 0x4003, (uint)key)) hotkeyFailures.Add(label);
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && !busy && !dialogOpen)
        {
            switch (m.WParam.ToInt32())
            {
                case 1: _ = CaptureTextAsync(); break;
                case 2: _ = CaptureImageAsync(); break;
                case 3: SaveWord(); break;
                case 4: ToggleMain(); break;
            }
        }
        base.WndProc(ref m);
    }
    protected override void OnHandleDestroyed(EventArgs e) { for (int i = 1; i <= 4; i++) Native.UnregisterHotKey(Handle, i); base.OnHandleDestroyed(e); }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { setupGuide?.Dispose(); DismissPopup(true); currentConversation.Clear(); sessionOriginal = ""; sessionTranslation = ""; tips.Dispose(); foregroundTracker.Dispose(); tray.Dispose(); floating.Dispose(); menu.Dispose(); api.Dispose(); dockIcon.Dispose(); }
        base.Dispose(disposing);
    }
}
