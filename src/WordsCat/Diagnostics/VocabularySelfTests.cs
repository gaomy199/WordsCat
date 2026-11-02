using System.Text.Json;

namespace ReadDock;

internal static class VocabularySelfTests
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception("Vocabulary regression: " + name); }
    internal static void Run(string directory)
    {
        var basic = VocabularyTemplate.Load(AppPaths.DefaultTemplate);
        var defaults = JsonSerializer.Deserialize<AppConfig>("{}", AppConfig.Json)!.Vocabulary;
        Check(defaults.SupplementSenses && !defaults.IncludePhonetics && !defaults.GenerateExamples && !defaults.IncludeCollocations, "legacy defaults");
        var off = new VocabularyOptions { SupplementSenses = false };
        Check(basic.WithOptions(off).Fields.Count == 6, "all off retains lean fields");
        var all = new VocabularyOptions { IncludePhonetics = true, GenerateExamples = true, IncludeCollocations = true };
        var template = basic.WithOptions(all);
        Check(template.Fields.Count == 11 && basic.Fields.Count == 6 && !basic.EntryTemplate.Contains("other_meanings"), "effective template leaves original intact");
        Check(template.WithOptions(all).Fields.Count == 11, "options are idempotent");
        foreach (var option in new[] {
            new VocabularyOptions { SupplementSenses = false, IncludePhonetics = true },
            new VocabularyOptions { SupplementSenses = false, GenerateExamples = true },
            new VocabularyOptions { SupplementSenses = false, IncludeCollocations = true } })
            Check(basic.WithOptions(option).Fields.Count == (option.GenerateExamples ? 8 : 7), "independent options");
        const string original = "Algorithms segment images.";
        string json = JsonSerializer.Serialize(new { word = "wrong", meaning = "分割", example = "We segment the market.", example_translation = "虚构原句", other_meanings = "n. 段；部分", phonetic = "/ˈseɡmənt/", generated_example = "A segment of the series is missing.", generated_example_translation = "这个系列缺了一部分。", collocations = "a series of — 一系列" });
        var values = Prompts.ParseWord(json, template, "segment", original);
        Check(values["word"] == "segment" && values["example"] == "" && values["example_translation"] == "" && values["generated_example"].Length > 0 && values["other_meanings"].Contains("n."), "AI content remains separate from verified source");
        var lean = Prompts.ParseWord(json, basic.WithOptions(off), "segment", original);
        Check(!lean.ContainsKey("phonetic") && !lean.ContainsKey("other_meanings") && !lean.ContainsKey("generated_example") && !lean.ContainsKey("collocations"), "disabled extras are not accepted");
        var prompt = Prompts.Word(template, "segment", original);
        Check(prompt[0].Content.Contains("不限于原句中的词性") && prompt.Count == 2 && prompt[1].Content.Contains("segment"), "broader senses requested for current selection only");
        Check(template.Render(new WordEntry { Values = values }).Contains("n. 段；部分"), "extra senses reach deterministic Markdown");
        var custom = new VocabularyTemplate { Fields = [new() { Key = "word", Required = true }, new() { Key = "custom", Label = "Custom" }], EntryTemplate = "{{word}} / {{custom}}" };
        Check(custom.WithOptions(all).EntryTemplate.StartsWith(custom.EntryTemplate) && custom.Fields.Count == 2, "custom layout and fields preserved");

        string savedData = AppPaths.DataDirectory;
        string root = Path.Combine(directory, "portable-paths"), before = Path.Combine(root, "before", "Data"), after = Path.Combine(root, "after", "Data");
        Directory.CreateDirectory(before);
        try
        {
            AppPaths.DataDirectory = before;
            string notebook = Path.Combine(before, "nested", "words.md"), customFile = Path.Combine(before, "custom.json");
            AtomicFile.Write(notebook, "existing notebook bytes\n");
            AtomicFile.Write(customFile, JsonSerializer.Serialize(custom, AppConfig.Json));
            byte[] bytes = File.ReadAllBytes(customFile);
            var cfg = new AppConfig { TemplateFile = customFile, TargetFile = notebook, Vocabulary = all }; cfg.Save();
            using (var stored = JsonDocument.Parse(File.ReadAllText(AppPaths.ConfigFile)))
                Check(stored.RootElement.GetProperty("template_file").GetString() == "custom.json" && stored.RootElement.GetProperty("target_file").GetString() == Path.Combine("nested", "words.md"), "settings store relative internal paths");
            Check(cfg.TemplateFile == customFile && cfg.ActiveNotebook.File == notebook, "runtime paths remain absolute");
            Directory.Move(Path.GetDirectoryName(before)!, Path.GetDirectoryName(after)!);
            AppPaths.DataDirectory = after;
            var loaded = AppConfig.Load();
            Check(loaded.TemplateFile == Path.Combine(after, "custom.json") && loaded.ActiveNotebook.File == Path.Combine(after, "nested", "words.md") && loaded.Vocabulary.GenerateExamples, "moved folder reload and options roundtrip");
            Check(File.ReadAllBytes(loaded.TemplateFile).SequenceEqual(bytes) && File.ReadAllText(loaded.ActiveNotebook.File) == "existing notebook bytes\n", "custom and notebook bytes unchanged");
            Check(PortablePaths.Resolve(notebook) == loaded.ActiveNotebook.File, "missing legacy Data absolute path recovers locally");
            string external = Path.Combine(Path.GetDirectoryName(root)!, "outside-vault", "words.md");
            Check(PortablePaths.Store(external, after, Path.GetDirectoryName(after)!) == external && PortablePaths.Resolve(external) == external, "external vault path preserved");
            string missingCustom = Path.Combine(root, "missing", "vocabulary.template.json");
            Check(PortablePaths.Resolve(missingCustom) == missingCustom, "missing custom template never silently replaced");
        }
        finally { AppPaths.DataDirectory = savedData; }
    }

    internal static async Task RunUiAsync()
    {
        var cfg = new AppConfig { TemplateFile = AppPaths.DefaultTemplate, UiLanguage = "zh-CN" };
        using (var settings = new SettingsDialog(cfg, useCredentials: false))
        {
            settings.Show(); await Task.Delay(80);
            foreach (var toggle in Children(settings).OfType<SlideSwitch>()) toggle.Checked = !toggle.Checked;
            settings.Close();
            Check(cfg.Vocabulary.SupplementSenses && !cfg.Vocabulary.GenerateExamples, "cancel discards switch drafts");
        }
        using (var settings = new SettingsDialog(cfg, useCredentials: false))
        {
            settings.Show();
            var toggles = Children(settings).OfType<SlideSwitch>().ToArray();
            Check(toggles.Length == 4 && toggles.All(t => t.AccessibleRole == AccessibleRole.CheckButton && t.TabStop), "four keyboard accessible switches");
            foreach (var toggle in toggles) toggle.Checked = true;
            Children(settings).OfType<Button>().Single(b => b.Text == UiText.T("保存设置")).PerformClick();
            Check(settings.IsDisposed && cfg.Vocabulary.IncludePhonetics && cfg.Vocabulary.IncludeCollocations && AppConfig.Load().Vocabulary.GenerateExamples, "save persists switches");
        }
        using var dialog = new WordDialog(cfg, new VocabularyStore(), new NeverCalledClient(), "segment", "Algorithms segment images.", () => "");
        dialog.Show(); await Task.Delay(80);
        Check(Children(dialog).OfType<TextBox>().Any(t => t.Name == "wordField_other_meanings") && Children(dialog).OfType<TextBox>().Any(t => t.Name == "wordField_generated_example"), "enabled fields appear in real editor");
        dialog.Close();
    }
    sealed class NeverCalledClient : ITextModelClient
    {
        public Task<ApiResult> CompleteAsync(ModelRequestOptions options, string key, IReadOnlyList<ChatMessage> messages, bool json, CancellationToken token) => throw new Exception("No request expected");
    }
    static IEnumerable<Control> Children(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (var child in Children(c)) yield return child; } }
}
