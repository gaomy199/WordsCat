using System.Text.Json;

namespace ReadDock;

internal sealed class SettingsDialog : Form
{
    private readonly AppConfig config;
    private readonly TextBox apiKey = Ui.TextBox();
    private readonly TextBox baseUrl = Ui.TextBox();
    private readonly ComboBox model = EditableCombo("deepseek-flash", "deepseek-v4-pro");
    private readonly ComboBox visionModel = EditableCombo("deepseek-flash");
    private readonly ComboBox engine = Choices(new("vision", "DeepSeek 视觉 · 数学与扫描文献"), new("local", "Windows 本地 OCR · 仅上传识别文字"));
    private readonly DataGridView notebooks = new();
    private readonly TextBox template = Ui.TextBox();
    private readonly TextBox instruction = Ui.TextBox(true);
    private readonly TextBox glossary = Ui.TextBox(true);
    private readonly ComboBox theme = Choices(new("light", "浅色"), new("dark", "深色"), new("system", "跟随系统"));
    private readonly ComboBox language = Choices(new("zh-CN", "简体中文"), new("en", "English"));
    private readonly TextBox accent = Ui.TextBox();
    private readonly TrackBar brightness = new() { Minimum = 0, Maximum = 100, TickFrequency = 25, Dock = DockStyle.Fill };
    private readonly TextBox logo = Ui.TextBox();
    private readonly PictureBox logoPreview = new() { Size = new Size(52, 52), SizeMode = PictureBoxSizeMode.Zoom };
    private readonly CheckBox floating = new() { Text = "显示悬浮图标", AutoSize = true, Font = Ui.Font() };
    private readonly CheckBox startup = new() { Text = "启动时打开主窗口", AutoSize = true, Font = Ui.Font() };
    private readonly Label testStatus = Ui.Label("", false, 9);
    private CancellationTokenSource? testCancellation;
    private bool notebookEvent;
    private readonly Action<AppConfig> applyAppearance;
    private readonly Action<string>? applyLanguage;
    private readonly bool useCredentials;
    private readonly ReadingHistory? historyStore;
    private readonly Func<string, bool> confirmHistoryReduction;
    private readonly NumericUpDown historyLimit = new() { Minimum = 0, Maximum = 1000, Dock = DockStyle.Fill, Font = Ui.Font() };
    private readonly Label historyNotice = Ui.Label("", false, 9);

    public SettingsDialog(AppConfig config, Action<AppConfig>? applyAppearance = null, bool useCredentials = true, ReadingHistory? historyStore = null, Func<string, bool>? confirmHistoryReduction = null, Action<string>? applyLanguage = null)
    {
        this.config = config;
        this.applyAppearance = applyAppearance ?? Ui.SetAppearance;
        this.applyLanguage = applyLanguage;
        this.useCredentials = useCredentials;
        this.historyStore = historyStore;
        this.confirmHistoryReduction = confirmHistoryReduction ?? (message => UiText.Show(this, message, "永久删除历史记录", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes);
        config.NormalizeNotebooks();
        Text = "单词猫 · 设置"; Font = Ui.Font(); BackColor = Ui.Paper; AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(820, 780); MinimumSize = new Size(720, 650);
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        apiKey.UseSystemPasswordChar = true; apiKey.Text = useCredentials ? CredentialStore.Read() : "";
        apiKey.Enabled = useCredentials;
        baseUrl.Text = config.BaseUrl; model.Text = config.Model; visionModel.Text = config.VisionModel;
        SelectChoice(engine, config.ScreenshotEngine); SelectChoice(theme, config.ThemeMode);
        SelectChoice(language, config.UiLanguage); language.AccessibleName = "uiLanguage";
        template.Text = config.TemplateFile; instruction.Text = config.TranslationInstruction; glossary.Text = config.Glossary;
        accent.Text = config.AccentColor; brightness.Value = Math.Clamp(config.BackgroundBrightness, 0, 100);
        logo.Text = config.LogoFile; floating.Checked = config.ShowFloating; startup.Checked = config.ShowOnStartup;
        historyLimit.Value = Math.Clamp(config.HistoryLimit, 0, 1000);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.Controls.Add(Ui.Label("让工具适应你的阅读习惯", true, 17), 0, 0);
        var tabs = new ThemedTabs { Dock = DockStyle.Fill, Font = Ui.Font() };
        var api = Page("模型与识别"); var book = Page("单词本与模板"); var appearance = Page("外观"); var read = Page("阅读偏好");
        tabs.TabPages.AddRange([api, book, appearance, read]);

        var ap = FormPanel(); api.Controls.Add(ap);
        AddField(ap, "DeepSeek API Key", apiKey);
        AddField(ap, "文本翻译模型（可编辑）", model);
        AddField(ap, "截图识别方式", engine);
        AddField(ap, "视觉识别模型（需要支持图片，默认 deepseek-flash）", visionModel);
        AddField(ap, "API 基础地址", baseUrl);
        AddNote(ap, "视觉模式：仅将本次框选图片发给所选 API，一次完成识别和翻译。\n本地模式：Windows OCR 识别后，仅发送文字；数学斜体及上下标可能误读。\nKey 保存在 Windows 凭据管理器；本地翻译历史条数可在阅读偏好调整。", 76);
        AddControl(ap, Ui.Row(Ui.Button("测试文本连接", async () => await TestAsync(), false, 140), Ui.Button("获取 API Key", () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://platform.deepseek.com/api_keys") { UseShellExecute = true }), false, 135)), 44);
        AddControl(ap, testStatus, 70);

        var bp = FormPanel(); book.Controls.Add(bp);
        AddNote(bp, "按领域添加多个 Markdown 文件。主窗口和收藏窗口都可切换；启动时使用默认单词本。", 40);
        ConfigureNotebookGrid(); AddControl(bp, notebooks, 200);
        AddControl(bp, Ui.Row(Ui.Button("添加单词本", AddNotebook, false, 135), Ui.Button("移除所选项", RemoveNotebook, false, 135)), 44);
        AddField(bp, "字段与排版模板 JSON", template);
        AddControl(bp, Ui.Row(Ui.Button("选择模板", ChooseTemplate), Ui.Button("编辑模板", EditTemplate), Ui.Button("检查模板", ValidateTemplate), Ui.Button("使用精简模板", ResetTemplate, false, 135)), 44);
        AddNote(bp, "默认卡片仅保留：单词/词组、原形、词性、语境释义、原句及译文。\n移除单词本只移除配置项，Markdown 文件保留。自定义模板不会自动覆盖。", 62);

        var ep = FormPanel(); appearance.Controls.Add(ep);
        AddField(ep, "界面语言", language);
        AddField(ep, "界面模式", theme);
        AddField(ep, "主题强调色（#RRGGBB）", accent);
        AddControl(ep, Ui.Row(Ui.Button("选择颜色", ChooseAccent), Ui.Button("预览外观", PreviewAppearance)), 44);
        AddField(ep, "背景亮度（左暗 / 右亮）", brightness, 50);
        AddField(ep, "自定义 Logo（PNG / JPG / ICO；应用于悬浮、托盘和窗口图标）", logo);
        AddControl(ep, Ui.Row(Ui.Button("选择图片", ChooseLogo), Ui.Button("恢复默认 Logo", () => { logo.Clear(); UpdateLogoPreview(); }, false, 145), logoPreview), 62);
        AddNote(ep, "语言和外观立即预览；保存后保留，取消则恢复。Logo 会复制到 Data/Appearance。", 48);
        UpdateLogoPreview();

        var rp = FormPanel(); read.Controls.Add(rp);
        AddField(rp, "保留最近 N 条翻译历史（默认 100；0 为关闭并清空）", historyLimit);
        AddControl(rp, historyNotice, 64);
        historyLimit.ValueChanged += (_, _) => UpdateHistoryNotice(); UpdateHistoryNotice();
        AddField(rp, "翻译要求", instruction, 112); AddField(rp, "当前阅读术语表（每行一条，例如：robustness = 稳健性）", glossary, 112);
        AddControl(rp, Ui.Row(floating, startup), 42);
        AddNote(rp, "Ctrl+Alt+D  划词翻译\nCtrl+Alt+S  截图翻译\nCtrl+Alt+W  收藏单词\nCtrl+Alt+R  显示 / 隐藏主窗口\n截图后先在选区附近显示轻量弹窗，点击「打开主界面」再展开详细操作。", 130);
        root.Controls.Add(Ui.Card(tabs), 0, 1);
        var actions = Ui.Row(Ui.Button("保存设置", Save, true, 120), Ui.Button("取消", () => Close(), false, 90)); actions.Padding = new Padding(0, 12, 0, 0);
        root.Controls.Add(actions, 0, 2);
        Controls.Add(root);
        FormClosing += (_, _) => testCancellation?.Cancel();
        FormClosed += (_, _) => { PreviewLanguage(config.UiLanguage); this.applyAppearance(config); logoPreview.Image?.Dispose(); };
        Ui.FinishLayout(this);
        theme.SelectedIndexChanged += (_, _) => ApplyLiveAppearance();
        language.SelectedIndexChanged += (_, _) => PreviewLanguage(Value(language));
        accent.TextChanged += (_, _) => ApplyLiveAppearance();
        brightness.ValueChanged += (_, _) => ApplyLiveAppearance();
        logo.TextChanged += (_, _) => { UpdateLogoPreview(); ApplyLiveAppearance(); };
    }

    private sealed record Choice(string Value, string Name) { public override string ToString() => UiText.T(Name); }
    void PreviewLanguage(string value)
    {
        UiText.Language = value;
        UiText.Apply(this);
        foreach (var choice in new[] { engine, theme, language }) choice.Refresh();
        UpdateHistoryNotice();
        applyLanguage?.Invoke(value);
    }
    void UpdateHistoryNotice()
    {
        int count = historyStore?.RemovalCount((int)historyLimit.Value) ?? 0;
        historyNotice.Text = UiText.F("仅保留已完成的原文和译文，截图不写入历史。\n保存后将永久删除最早的 {0} 条记录，软件内无法恢复；已收藏单词不受影响。", count);
    }
    static ComboBox Choices(params Choice[] choices) { var c = new SoftComboBox { Dock = DockStyle.Fill, Font = Ui.Font(), DropDownStyle = ComboBoxStyle.DropDownList }; c.Items.AddRange(choices); c.SelectedIndex = 0; return c; }
    static ComboBox EditableCombo(params string[] items) { var c = new SoftComboBox { Dock = DockStyle.Fill, Font = Ui.Font(), DropDownStyle = ComboBoxStyle.DropDown }; c.Items.AddRange(items); return c; }
    static string Value(ComboBox c) => (c.SelectedItem as Choice)?.Value ?? "";
    static void SelectChoice(ComboBox c, string value) { foreach (Choice item in c.Items) if (item.Value == value) { c.SelectedItem = item; return; } }
    static TabPage Page(string title) => new(title) { BackColor = Ui.Surface, Padding = new Padding(14, 18, 14, 12), Tag = title switch { "模型与识别" => "model", "单词本与模板" => "book", "外观" => "appearance", _ => "reading" } };
    static TableLayoutPanel FormPanel() { var p = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 1, RowCount = 0, Padding = new Padding(0, 0, 14, 0) }; p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); return p; }
    static void AddControl(TableLayoutPanel p, Control c, int height) { int row = p.RowCount++; p.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); p.Controls.Add(c, 0, row); }
    static void AddField(TableLayoutPanel p, string text, Control c, int height = 46)
    {
        var label = Ui.Label(text, true, 9); label.Margin = new Padding(2, 3, 0, 2);
        AddControl(p, label, 28);
        int width = c.AccessibleName == "uiLanguage" || c is ComboBox combo && combo.Items.Count == 3 ? 290
            : c is NumericUpDown ? 180 : c is ComboBox { DropDownStyle: ComboBoxStyle.DropDown } ? 450 : 0;
        if (c is TextBoxBase or ComboBox or NumericUpDown) AddControl(p, new FieldFrame(c, width), height);
        else AddControl(p, c, height);
    }
    static void AddNote(TableLayoutPanel p, string text, int height) => AddControl(p, Ui.Label(text, false, 9), height);

    void ConfigureNotebookGrid()
    {
        notebooks.Dock = DockStyle.Fill; notebooks.Font = Ui.Font(9); notebooks.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        notebooks.AllowUserToAddRows = false; notebooks.AllowUserToDeleteRows = false; notebooks.AllowUserToResizeRows = false;
        notebooks.MultiSelect = false; notebooks.SelectionMode = DataGridViewSelectionMode.FullRowSelect; notebooks.RowHeadersVisible = false;
        notebooks.BorderStyle = BorderStyle.FixedSingle; notebooks.RowTemplate.Height = 30;
        notebooks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "名称", FillWeight = 24 });
        notebooks.Columns.Add(new DataGridViewTextBoxColumn { Name = "File", HeaderText = "Markdown 文件路径", FillWeight = 62 });
        notebooks.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Default", HeaderText = "默认", FillWeight = 10 });
        notebooks.Columns.Add(new DataGridViewButtonColumn { Name = "Pick", HeaderText = "", Text = "选择文件", UseColumnTextForButtonValue = true, FillWeight = 18 });
        foreach (var n in config.Notebooks)
        {
            int i = notebooks.Rows.Add(n.Name, n.File, n.Id == config.DefaultNotebookId); notebooks.Rows[i].Tag = n.Id;
        }
        notebooks.CurrentCellDirtyStateChanged += (_, _) => { if (notebooks.IsCurrentCellDirty) notebooks.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        notebooks.CellValueChanged += (_, e) =>
        {
            if (notebookEvent || e.RowIndex < 0 || e.ColumnIndex != 2 || notebooks.Rows[e.RowIndex].Cells[2].Value is not true) return;
            notebookEvent = true;
            foreach (DataGridViewRow row in notebooks.Rows) if (row.Index != e.RowIndex) row.Cells[2].Value = false;
            notebookEvent = false;
        };
        notebooks.CellContentClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == 3) BrowseNotebook(e.RowIndex); };
        notebooks.DataError += (_, e) => { e.ThrowException = false; };
    }
    void AddNotebook()
    {
        if (notebooks.Rows.Count >= 40) { UiText.Show(this, "最多添加 40 个单词本。", "单词本"); return; }
        int number = 1; var names = notebooks.Rows.Cast<DataGridViewRow>().Select(r => Convert.ToString(r.Cells[0].Value)).ToHashSet();
        while (names.Contains(UiText.T("单词本 ") + number)) number++;
        int index = notebooks.Rows.Add(UiText.T("单词本 ") + number, Path.Combine(AppPaths.DataDirectory, "vocabulary-" + number + ".md"), notebooks.Rows.Count == 0);
        notebooks.Rows[index].Tag = Guid.NewGuid().ToString("N"); notebooks.CurrentCell = notebooks.Rows[index].Cells[0];
    }
    void RemoveNotebook()
    {
        if (notebooks.CurrentRow == null) return;
        bool wasDefault = notebooks.CurrentRow.Cells[2].Value is true;
        notebooks.Rows.Remove(notebooks.CurrentRow);
        if (wasDefault && notebooks.Rows.Count > 0) notebooks.Rows[0].Cells[2].Value = true;
    }
    void BrowseNotebook(int index)
    {
        string path = Convert.ToString(notebooks.Rows[index].Cells[1].Value) ?? "";
        using var d = new SaveFileDialog { Filter = UiText.T("Markdown 笔记 (*.md)|*.md"), DefaultExt = "md", OverwritePrompt = false, FileName = Path.GetFileName(path), Title = UiText.T("选择或新建单词本（保留已有内容）") };
        if (Directory.Exists(Path.GetDirectoryName(path))) d.InitialDirectory = Path.GetDirectoryName(path);
        if (d.ShowDialog(this) == DialogResult.OK) notebooks.Rows[index].Cells[1].Value = d.FileName;
    }
    void ChooseTemplate() { using var d = new OpenFileDialog { Filter = UiText.T("JSON 模板 (*.json)|*.json"), CheckFileExists = true }; if (d.ShowDialog(this) == DialogResult.OK) template.Text = d.FileName; }
    void EditTemplate() { try { var p = new System.Diagnostics.ProcessStartInfo("notepad.exe"); p.ArgumentList.Add(template.Text); System.Diagnostics.Process.Start(p); } catch (Exception ex) { Ui.Error(this, ex); } }
    void ValidateTemplate() { try { var t = VocabularyTemplate.Load(template.Text.Trim()); UiText.Show(this, UiText.F("模板有效，共 {0} 个字段。\n", t.Fields.Count) + string.Join("、", t.Fields.Select(f => f.Label)), "模板检查"); } catch (Exception ex) { Ui.Error(this, ex); } }
    void ResetTemplate() { template.Text = AppPaths.DefaultTemplate; UiText.Show(this, "已选择随新版提供的精简模板。点击「保存设置」后生效。你原来的模板文件会保留。", "精简模板"); }
    void ChooseAccent() { try { using var d = new ColorDialog { Color = Ui.ParseAccent(accent.Text.Trim()), FullOpen = true }; if (d.ShowDialog(this) == DialogResult.OK) accent.Text = $"#{d.Color.R:X2}{d.Color.G:X2}{d.Color.B:X2}"; } catch (Exception ex) { Ui.Error(this, ex); } }
    void ChooseLogo()
    {
        using var d = new OpenFileDialog { Filter = UiText.T("图片 (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico"), CheckFileExists = true };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        try { using var check = Ui.LoadLogo(d.FileName); logo.Text = d.FileName; UpdateLogoPreview(); } catch (Exception ex) { Ui.Error(this, ex); }
    }
    void UpdateLogoPreview() { try { var old = logoPreview.Image; logoPreview.Image = Ui.LoadLogo(logo.Text) ?? Ui.CreateDefaultLogo(); old?.Dispose(); } catch { } }
    AppConfig AppearanceDraft()
    {
        Ui.ParseAccent(accent.Text.Trim());
        return new AppConfig { ThemeMode = Value(theme), AccentColor = accent.Text.Trim(), BackgroundBrightness = brightness.Value, LogoFile = logo.Text.Trim() };
    }
    void ApplyLiveAppearance()
    {
        // Incomplete color/path input is expected while typing; keep the last valid preview.
        try { applyAppearance(AppearanceDraft()); Ui.ApplyTheme(this); }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException or System.Runtime.InteropServices.ExternalException) { }
    }
    void PreviewAppearance()
    {
        try
        {
            Ui.SetAppearance(AppearanceDraft()); Ui.ApplyTheme(this);
            using var popup = new TranslationPopup("外观预览", () => { }, () => { }, () => { });
            popup.StartPosition = FormStartPosition.CenterParent;
            popup.SetResult(UiText.T("这里将显示你框选区域的中文译文。\n\n数学变量示例：q₁、q₂、…、qₖ；p₁、p₂、…、pₖ。"), "界面示例 · 未调用 API");
            popup.ShowDialog(this);
        }
        catch (Exception ex) { Ui.Error(this, ex); }
        finally { ApplyLiveAppearance(); }
    }
    AppConfig Proposed()
    {
        if (!Uri.TryCreate(baseUrl.Text.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidOperationException("请输入有效的 HTTPS 基础地址。");
        if (string.IsNullOrWhiteSpace(model.Text) || string.IsNullOrWhiteSpace(visionModel.Text)) throw new InvalidOperationException("请填写文本模型与视觉模型名称。");
        var c = JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(config, AppConfig.Json), AppConfig.Json)!;
        c.BaseUrl = baseUrl.Text.Trim().TrimEnd('/'); c.Model = model.Text.Trim(); c.VisionModel = visionModel.Text.Trim(); c.ScreenshotEngine = Value(engine);
        c.ThemeMode = Value(theme); c.AccentColor = accent.Text.Trim(); Ui.ParseAccent(c.AccentColor); c.BackgroundBrightness = brightness.Value; c.LogoFile = logo.Text.Trim();
        c.TemplateFile = Path.GetFullPath(template.Text.Trim()); VocabularyTemplate.Load(c.TemplateFile);
        c.TranslationInstruction = instruction.Text.Trim(); c.Glossary = glossary.Text.Trim(); c.ShowFloating = floating.Checked; c.ShowOnStartup = startup.Checked;
        c.HistoryLimit = (int)historyLimit.Value;
        c.UiLanguage = Value(language);
        notebooks.EndEdit();
        c.Notebooks = []; c.DefaultNotebookId = ""; c.ActiveNotebookId = config.ActiveNotebookId;
        foreach (DataGridViewRow row in notebooks.Rows)
        {
            var n = new NotebookTarget { Id = row.Tag as string ?? Guid.NewGuid().ToString("N"), Name = Convert.ToString(row.Cells[0].Value)?.Trim() ?? "", File = Convert.ToString(row.Cells[1].Value)?.Trim() ?? "" };
            c.Notebooks.Add(n); if (row.Cells[2].Value is true) c.DefaultNotebookId = n.Id;
        }
        if (c.Notebooks.Count == 0) throw new InvalidOperationException("请至少保留一个单词本。");
        c.NormalizeNotebooks(); return c;
    }
    async Task TestAsync()
    {
        if (!useCredentials) { testStatus.Text = "离线预览不读取凭据，也不发起模型请求。"; return; }
        if (testCancellation != null) return;
        testCancellation = new CancellationTokenSource(); testStatus.Text = "正在测试文本模型，会发起一次简短 API 请求…";
        try
        {
            var c = new AppConfig { BaseUrl = baseUrl.Text.Trim().TrimEnd('/'), Model = model.Text.Trim() };
            using var client = new DeepSeekClient();
            string storedKey = CredentialStore.Read();
            if (apiKey.Text.Trim() != storedKey) throw new InvalidOperationException("请先保存 API Key，再测试已保存的凭据。");
            var r = await client.CompleteAsync(c, storedKey, [new("system", "Translate the user word into Simplified Chinese. Output only the translation."), new("user", "reading")], false, testCancellation.Token);
            if (!IsDisposed) testStatus.Text = UiText.F("连接成功 · {0}\n输入 {1} / 输出 {2} tokens；截图视觉能力请用实际框选验证。", r.Text, r.InputTokens, r.OutputTokens);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!IsDisposed) testStatus.Text = SecretRedactor.Redact(ex.Message); }
        finally { testCancellation?.Dispose(); testCancellation = null; }
    }
    void Save()
    {
        try
        {
            SecretRedactor.Register(apiKey.Text);
            var c = Proposed();
            if (c.HistoryLimit < config.HistoryLimit)
            {
                int count = historyStore?.RemovalCount(c.HistoryLimit) ?? 0;
                string message = UiText.F("历史上限从 {0} 改为 {1}。\n将永久删除最早的 {2} 条本地记录{3}。\n删除后软件内无法恢复；已收藏的单词和 Markdown 不受影响。\n是否保存此设置？", config.HistoryLimit, c.HistoryLimit, count, c.HistoryLimit == 0 ? UiText.T("，并关闭历史保存") : "");
                if (!confirmHistoryReduction(message)) return;
            }
            if (!string.IsNullOrWhiteSpace(c.LogoFile))
            {
                using var image = Ui.LoadLogo(c.LogoFile) ?? throw new InvalidDataException("Logo 文件不存在，请重新选择。");
                var folder = Path.Combine(AppPaths.DataDirectory, "Appearance"); Directory.CreateDirectory(folder);
                string stored = Path.Combine(folder, "logo.png");
                if (!string.Equals(Path.GetFullPath(c.LogoFile), Path.GetFullPath(stored), StringComparison.OrdinalIgnoreCase))
                { var temp = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".png"); try { image.Save(temp, System.Drawing.Imaging.ImageFormat.Png); File.Move(temp, stored, true); } finally { if (File.Exists(temp)) File.Delete(temp); } }
                c.LogoFile = stored;
            }
            c.Save(); if (useCredentials) CredentialStore.Save(apiKey.Text.Trim());
            if (c.HistoryLimit == 0) historyStore?.Clear(); else historyStore?.Trim(c.HistoryLimit);
            config.BaseUrl = c.BaseUrl; config.Model = c.Model; config.VisionModel = c.VisionModel; config.ScreenshotEngine = c.ScreenshotEngine;
            config.Notebooks = c.Notebooks; config.DefaultNotebookId = c.DefaultNotebookId; config.ActiveNotebookId = c.ActiveNotebookId; config.TargetFile = c.TargetFile;
            config.TemplateFile = c.TemplateFile; config.TranslationInstruction = c.TranslationInstruction; config.Glossary = c.Glossary;
            config.ShowFloating = c.ShowFloating; config.ShowOnStartup = c.ShowOnStartup; config.ThemeMode = c.ThemeMode; config.AccentColor = c.AccentColor; config.BackgroundBrightness = c.BackgroundBrightness; config.LogoFile = c.LogoFile;
            config.HistoryLimit = c.HistoryLimit;
            config.UiLanguage = c.UiLanguage;
            DialogResult = DialogResult.OK; Close();
        }
        catch (Exception ex) { Ui.Error(this, ex); }
    }
}
