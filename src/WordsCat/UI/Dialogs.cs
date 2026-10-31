namespace ReadDock;

internal sealed class WordDialog : Form
{
    private readonly AppConfig config;
    private readonly VocabularyTemplate template;
    private readonly VocabularyStore store;
    private readonly DeepSeekClient api;
    private readonly Func<string> readKey;
    private readonly Dictionary<string, TextBox> editors = [];
    private readonly string original;
    private readonly Label status = Ui.Label("收藏前可以修改所有字段。", false, 9);
    private readonly TextBox preview = Ui.TextBox(true);
    private readonly Button saveButton;
    private readonly Button aiButton;
    private readonly TableLayoutPanel form;
    private readonly ComboBox notebookChoice = new SoftComboBox() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = Ui.Font() };
    private CancellationTokenSource? cancellation;
    public bool Saved { get; private set; }
    public WordDialog(AppConfig config, VocabularyStore store, DeepSeekClient api, string word, string original, Func<string>? readKey = null)
    {
        this.config = config; this.store = store; this.api = api; this.original = original;
        this.readKey = readKey ?? CredentialStore.Read;
        template = VocabularyTemplate.Load(config.TemplateFile);
        Text = "单词猫 · 收藏生词"; Font = Ui.Font(); BackColor = Ui.Paper; AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(700, 740); MinimumSize = new Size(620, 580); StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 6 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.Controls.Add(Ui.Label("把语境一起留下", true, 18), 0, 0); root.Controls.Add(Ui.Label("释义结合原句 · 按你的模板保存到 Obsidian", false, 9), 0, 1);
        var tabs = new ThemedTabs { Dock = DockStyle.Fill, Font = Ui.Font() };
        var editPage = new TabPage("词条内容") { BackColor = Ui.Surface, Padding = new Padding(12) };
        var previewPage = new TabPage("Markdown 预览") { BackColor = Ui.Surface, Padding = new Padding(12) };
        preview.ReadOnly = true; preview.Font = new Font("Consolas", 10); previewPage.Controls.Add(preview);
        tabs.TabPages.AddRange([editPage, previewPage]);
        form = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 2, RowCount = 0, Padding = new Padding(0, 0, 16, 0) };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135)); form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var f in template.Fields)
        {
            int r = form.RowCount++; form.RowStyles.Add(new RowStyle(SizeType.Absolute, f.Multiline ? 88 : 48));
            form.Controls.Add(Ui.Label(f.Label + (f.Required ? " *" : ""), true, 9), 0, r);
            var box = Ui.TextBox(f.Multiline); box.Margin = new Padding(0, 5, 0, 9); editors[f.Key] = box; form.Controls.Add(box, 1, r);
            box.TextChanged += (_, _) => UpdatePreview();
        }
        editPage.Controls.Add(form);
        editors["word"].Text = word;
        var notebookRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        notebookRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80)); notebookRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var n in config.Notebooks) notebookChoice.Items.Add(n);
        notebookChoice.SelectedItem = config.ActiveNotebook;
        notebookRow.Controls.Add(Ui.Label("保存到", true, 9), 0, 0); notebookRow.Controls.Add(notebookChoice, 1, 0); root.Controls.Add(notebookRow, 0, 2);
        root.Controls.Add(tabs, 0, 3); status.Dock = DockStyle.Fill; root.Controls.Add(status, 0, 4);
        saveButton = Ui.Button("保存到单词本", Save, true, 135);
        aiButton = Ui.Button("AI 补全释义", async () => await FillAsync(), false, 130);
        var cancel = Ui.Button("取消", () => Close(), false, 80);
        root.Controls.Add(Ui.Row(saveButton, aiButton, cancel), 0, 5); Controls.Add(root);
        UpdatePreview();
        Shown += async (_, _) => { if (!string.IsNullOrWhiteSpace(word) && !string.IsNullOrWhiteSpace(this.readKey())) await FillAsync(); };
        FormClosing += (_, _) => cancellation?.Cancel();
        Ui.FinishLayout(this);
    }
    WordEntry Entry() => new() { Values = editors.ToDictionary(kv => kv.Key, kv => kv.Value.Text.Trim()) };
    void UpdatePreview() { if (preview != null) preview.Text = template.Render(Entry()).Replace("\n", Environment.NewLine); }
    async Task FillAsync()
    {
        if (cancellation != null) return;
        if (string.IsNullOrWhiteSpace(editors["word"].Text)) { status.Text = "请先填写要收藏的单词或词组。"; return; }
        cancellation = new CancellationTokenSource(); form.Enabled = false; saveButton.Enabled = false; aiButton.Enabled = false; status.Text = "正在结合当前原文补全释义…";
        try
        {
            var word = editors["word"].Text.Trim();
            var r = await api.CompleteAsync(config, readKey(), Prompts.Word(template, word, original), true, cancellation.Token);
            var values = Prompts.ParseWord(r.Text, template, word, original);
            if (IsDisposed) return;
            foreach (var (k, v) in values)
            {
                if (k is "source" or "page" or "notes") continue;
                if (editors.TryGetValue(k, out var box)) box.Text = v;
            }
            status.Text = UiText.F("已补全，请核对原句和释义后保存。输入 {0} / 输出 {1} tokens", r.InputTokens, r.OutputTokens);
            if (editors.TryGetValue("example", out var e) && e.Text.Length == 0) status.Text = "没有可核验的文献原句，已留空。可以手动补充后保存。";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!IsDisposed) status.Text = SecretRedactor.Redact(UiText.T(ex.Message)) + UiText.T(" 你仍可手动填写并保存。"); }
        finally { cancellation?.Dispose(); cancellation = null; if (!IsDisposed) { form.Enabled = true; saveButton.Enabled = true; aiButton.Enabled = true; } }
    }
    void Save()
    {
        try
        {
            var entry = Entry();
            var target = notebookChoice.SelectedItem as NotebookTarget ?? config.ActiveNotebook;
            bool duplicate = store.HasWord(target.File, entry.Values["word"]);
            bool allow = false;
            if (duplicate)
            {
                allow = UiText.Show(this, "这个单词已经收藏过。\n\n是否保留本次语境，追加一张新卡片？\n原有卡片会保留。选择「否」可返回修改或取消。", "发现重复单词", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
                if (!allow) return;
            }
            store.Save(target.File, template, entry, allow); config.ActiveNotebookId = target.Id; Saved = true; DialogResult = DialogResult.OK; Close();
        }
        catch (DuplicateWordException) { status.Text = "保存时发现重复单词，请再次点击保存选择处理方式。"; }
        catch (IOException) { status.Text = "文件正被占用或无法写入。请检查路径、同步状态和文件权限后重试。"; }
        catch (Exception ex) { Ui.Error(this, ex); }
    }
}
