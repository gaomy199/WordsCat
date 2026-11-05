namespace ReadDock;

internal sealed class WordDialog : Form
{
    private readonly AppConfig config;
    private VocabularyTemplate template;
    private readonly VocabularyTemplate initialTemplate;
    private readonly HashSet<string> activeSupplements = [];
    private readonly VocabularyStore store;
    private readonly ITextModelClient api;
    private readonly Func<string> readKey;
    private readonly Dictionary<string, TextBox> editors = [];
    private readonly string original;
    private readonly Label status = Ui.Label("收藏前可以修改所有字段。", false, 9);
    private readonly TextBox preview = Ui.TextBox(true);
    private readonly Button saveButton;
    private readonly List<Button> supplementButtons = [];
    private readonly TableLayoutPanel form;
    private readonly Panel fieldScroll;
    private readonly ComboBox notebookChoice = new SoftComboBox() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = Ui.Font() };
    private CancellationTokenSource? cancellation;
    public bool Saved { get; private set; }
    public WordDialog(AppConfig config, VocabularyStore store, ITextModelClient api, string word, string original, Func<string>? readKey = null)
    {
        this.config = config; this.store = store; this.api = api; this.original = original;
        this.readKey = readKey ?? (() => CredentialStore.ReadFor(config.BaseUrl));
        initialTemplate = template = VocabularyTemplate.Load(config.TemplateFile).WithOptions(config.Vocabulary);
        Text = "单词猫 · 收藏生词"; Font = Ui.Font(); BackColor = Ui.Paper; AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        ClientSize = new Size(700, 740); MinimumSize = new Size(640, 580); StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 6 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.Controls.Add(Ui.Label("把语境一起留下", true, 18), 0, 0); root.Controls.Add(Ui.Label("释义结合原句 · 按你的模板保存到 Obsidian", false, 9), 0, 1);
        var tabs = new ThemedTabs { Dock = DockStyle.Fill, Font = Ui.Font() };
        var editPage = new TabPage("词条内容") { BackColor = Ui.Surface, Padding = new Padding(12) };
        var previewPage = new TabPage("Markdown 预览") { BackColor = Ui.Surface, Padding = new Padding(12) };
        preview.ReadOnly = true; preview.Font = new Font("Consolas", 10); previewPage.Controls.Add(preview);
        tabs.TabPages.AddRange([editPage, previewPage]);
        fieldScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, 0, 16, 0), Name = "wordFieldScroll" };
        // The scroll viewport must not stretch the final field into leftover virtual space.
        form = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 0, Margin = Padding.Empty };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135)); form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fieldScroll.Controls.Add(form);
        RebuildFields(false);
        editPage.Controls.Add(fieldScroll);
        editors["word"].Text = word;
        var notebookRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        notebookRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80)); notebookRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var n in config.Notebooks) notebookChoice.Items.Add(n);
        notebookChoice.SelectedItem = config.ActiveNotebook;
        notebookRow.Controls.Add(Ui.Label("保存到", true, 9), 0, 0); notebookRow.Controls.Add(notebookChoice, 1, 0); root.Controls.Add(notebookRow, 0, 2);
        root.Controls.Add(tabs, 0, 3); status.Dock = DockStyle.Fill; root.Controls.Add(status, 0, 4);
        saveButton = Ui.Button("保存到单词本", Save, true, 135);
        var phonetic = SupplementButton("补全音标", new VocabularyOptions { IncludeWordForms = false, IncludePhonetics = true }, 100);
        var examples = SupplementButton("AI 例句", new VocabularyOptions { IncludeWordForms = false, GenerateExamples = true }, 90);
        var collocations = SupplementButton("补充常见搭配", new VocabularyOptions { IncludeWordForms = false, IncludeCollocations = true }, 120);
        var cancel = Ui.Button("取消", () => Close(), false, 80);
        root.Controls.Add(Ui.Row(saveButton, phonetic, examples, collocations, cancel), 0, 5); Controls.Add(root);
        UpdatePreview();
        Shown += async (_, _) => { if (!string.IsNullOrWhiteSpace(word) && !string.IsNullOrWhiteSpace(this.readKey())) await FillAsync(); };
        FormClosing += (_, _) => cancellation?.Cancel();
        Ui.FinishLayout(this);
    }
    WordEntry Entry() => new() { Values = editors.Where(pair => template.Fields.Any(field => field.Key == pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value.Text.Trim()) };
    void UpdatePreview() { if (preview != null) preview.Text = template.Render(Entry()).Replace("\n", Environment.NewLine); }
    void RebuildFields(bool scaled)
    {
        float scale = scaled ? DeviceDpi / 96f : 1f;
        fieldScroll.SuspendLayout(); fieldScroll.AutoScrollPosition = Point.Empty;
        form.SuspendLayout();
        try
        {
            var labels = form.Controls.OfType<Label>().ToArray();
            form.Controls.Clear(); foreach (var label in labels) label.Dispose();
            foreach (var key in editors.Keys.Where(key => !template.Fields.Any(field => field.Key == key)).ToArray())
            { editors[key].Dispose(); editors.Remove(key); }
            form.RowStyles.Clear(); form.RowCount = 0;
            foreach (var field in template.Fields)
            {
                int row = form.RowCount++; form.RowStyles.Add(new RowStyle(SizeType.Absolute, (field.Multiline ? 88 : 48) * scale));
                var label = Ui.Label(field.Label + (field.Required ? " *" : ""), true, 9);
                label.Margin = new Padding(0, (int)(4 * scale), 0, (int)(4 * scale));
                form.Controls.Add(label, 0, row);
                if (!editors.TryGetValue(field.Key, out var box))
                {
                    box = Ui.TextBox(field.Multiline); box.Name = "wordField_" + field.Key;
                    box.Margin = new Padding(0, (int)(5 * scale), 0, (int)(9 * scale));
                    box.BackColor = Ui.Surface; box.ForeColor = Ui.Ink;
                    editors[field.Key] = box; box.TextChanged += (_, _) => UpdatePreview();
                }
                form.Controls.Add(box, 1, row);
            }
            form.Height = (int)Math.Ceiling(form.RowStyles.Cast<RowStyle>().Sum(row => row.Height));
        }
        finally { form.ResumeLayout(true); fieldScroll.ResumeLayout(true); }
        UiText.Apply(form);
    }
    Button SupplementButton(string text, VocabularyOptions options, int width)
    {
        var keys = options.Fields().Select(field => field.Key).ToArray();
        string group = keys[0];
        if (template.Fields.Any(field => keys.Contains(field.Key))) activeSupplements.Add(group);
        Button button = Ui.Button(text, () => { }, false, width);
        Ui.SetButtonSelected(button, activeSupplements.Contains(group));
        button.Click += async (_, _) =>
        {
            if (cancellation != null) return;
            bool removing = activeSupplements.Contains(group);
            if (!removing && string.IsNullOrWhiteSpace(editors["word"].Text)) { status.Text = "请先填写要收藏的单词或词组。"; return; }
            if (removing) activeSupplements.Remove(group); else activeSupplements.Add(group);
            var selected = new VocabularyOptions
            {
                IncludeWordForms = false,
                IncludePhonetics = activeSupplements.Contains("phonetic"),
                GenerateExamples = activeSupplements.Contains("generated_example"),
                IncludeCollocations = activeSupplements.Contains("collocations")
            };
            var optionalKeys = new VocabularyOptions { IncludeWordForms = false, IncludePhonetics = true, GenerateExamples = true, IncludeCollocations = true }.Fields().Select(field => field.Key);
            var enabledKeys = selected.Fields().Select(field => field.Key).ToHashSet();
            // Rebuild only this dialog's template, including removal from Markdown preview/save.
            template = initialTemplate.WithOptions(selected).WithoutFields(optionalKeys.Where(key => !enabledKeys.Contains(key)).ToArray());
            RebuildFields(true); UpdatePreview(); Ui.SetButtonSelected(button, !removing);
            if (removing) { status.Text = "已取消本次词条的所选补充内容。"; return; }
            await FillAsync(keys);
            if (!IsDisposed && editors.TryGetValue(keys[0], out var field)) fieldScroll.ScrollControlIntoView(field);
        };
        supplementButtons.Add(button); return button;
    }
    async Task FillAsync(IReadOnlyCollection<string>? selectedKeys = null)
    {
        if (cancellation != null) return;
        if (string.IsNullOrWhiteSpace(editors["word"].Text)) { status.Text = "请先填写要收藏的单词或词组。"; return; }
        cancellation = new CancellationTokenSource(); form.Enabled = false; saveButton.Enabled = false;
        foreach (var button in supplementButtons) button.Enabled = false;
        status.Text = selectedKeys == null ? "正在结合当前原文补全释义…" : "正在补充本次词条的所选内容…";
        try
        {
            var word = editors["word"].Text.Trim();
            var requestTemplate = selectedKeys == null ? template : new VocabularyTemplate
            {
                Fields = template.Fields.Where(field => field.Key == "word" || selectedKeys.Contains(field.Key)).ToList()
            };
            var r = await api.CompleteAsync(config, readKey(), Prompts.Word(requestTemplate, word, original), true, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            var values = Prompts.ParseWord(r.Text, requestTemplate, word, original);
            if (IsDisposed) return;
            foreach (var (k, v) in values)
            {
                if (k is "source" or "page" or "notes") continue;
                if (selectedKeys != null && !selectedKeys.Contains(k)) continue;
                if (editors.TryGetValue(k, out var box)) box.Text = v;
            }
            status.Text = UiText.F(selectedKeys == null ? "已补全，请核对原句和释义后保存。输入 {0} / 输出 {1} tokens" : "已补充本次词条内容，可编辑后保存。输入 {0} / 输出 {1} tokens", r.InputTokens, r.OutputTokens);
            if (selectedKeys == null && editors.TryGetValue("example", out var e) && e.Text.Length == 0) status.Text = "没有可核验的文献原句，已留空。可以手动补充后保存。";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!IsDisposed) status.Text = SecretRedactor.Redact(UiText.T(ex.Message)) + UiText.T(" 你仍可手动填写并保存。"); }
        finally { cancellation?.Dispose(); cancellation = null; if (!IsDisposed) { form.Enabled = true; saveButton.Enabled = true; foreach (var button in supplementButtons) button.Enabled = true; } }
    }
    void Save()
    {
        try
        {
            var entry = Entry();
            var target = notebookChoice.SelectedItem as NotebookTarget ?? config.ActiveNotebook;
            bool duplicate = store.HasWord(target.File, entry.Values["word"]);
            bool replace = false;
            if (duplicate)
            {
                replace = UiText.Show(this, "这个单词已经收藏过。\n\n是否用本次词条覆盖当前单词本中的旧词条？\n选择「是」替换旧内容；选择「否」返回修改。", "发现重复单词", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
                if (!replace) return;
            }
            store.Save(target.File, template, entry, false, replaceDuplicate: replace); config.ActiveNotebookId = target.Id; Saved = true; DialogResult = DialogResult.OK; Close();
        }
        catch (DuplicateWordException) { status.Text = "保存时发现重复单词，请再次点击保存选择处理方式。"; }
        catch (IOException) { status.Text = "文件正被占用或无法写入。请检查路径、同步状态和文件权限后重试。"; }
        catch (Exception ex) { Ui.Error(this, ex); }
    }
}
