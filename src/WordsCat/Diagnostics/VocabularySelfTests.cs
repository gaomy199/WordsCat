using System.Text.Json;

namespace ReadDock;

internal static class VocabularySelfTests
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception("Vocabulary regression: " + name); }
    internal static void Run(string directory)
    {
        var basic = VocabularyTemplate.Load(AppPaths.DefaultTemplate);
        var defaults = JsonSerializer.Deserialize<AppConfig>("{}", AppConfig.Json)!.Vocabulary;
        Check(defaults.IncludeWordForms && !defaults.IncludePhonetics && !defaults.GenerateExamples && !defaults.IncludeCollocations, "legacy defaults");
        var off = new VocabularyOptions { IncludeWordForms = false };
        Check(basic.WithOptions(off).Fields.Count == 6, "all off retains lean fields");
        var all = new VocabularyOptions { IncludePhonetics = true, GenerateExamples = true, IncludeCollocations = true };
        var template = basic.WithOptions(all);
        Check(template.Fields.Count == 11 && basic.Fields.Count == 6 && !basic.EntryTemplate.Contains("word_forms"), "effective template leaves original intact");
        Check(template.WithOptions(all).Fields.Count == 11, "options are idempotent");
        foreach (var option in new[] {
            new VocabularyOptions { IncludeWordForms = false, IncludePhonetics = true },
            new VocabularyOptions { IncludeWordForms = false, GenerateExamples = true },
            new VocabularyOptions { IncludeWordForms = false, IncludeCollocations = true } })
            Check(basic.WithOptions(option).Fields.Count == (option.GenerateExamples ? 8 : 7), "independent options");
        const string original = "Algorithms segment images.";
        string json = JsonSerializer.Serialize(new { word = "wrong", meaning = "v. 分割；n. 段、部分", example = "We segment the market.", example_translation = "虚构原句", word_forms = "segmentation [n.] 分割\nsegmented [adj.] 分段的\nsegmenting [v.] 分割\nsegments [n.] 部分", phonetic = "/ˈseɡmənt/", generated_example = "A segment of the series is missing.", generated_example_translation = "这个系列缺了一部分。", collocations = "a series of — 一系列" });
        var values = Prompts.ParseWord(json, template, "segment", original);
        Check(values["word"] == "segment" && values["example"] == "" && values["example_translation"] == "" && values["generated_example"].Length > 0 && values["word_forms"].Split('\n').Length == 3, "AI content remains separate from verified source");
        var lean = Prompts.ParseWord(json, basic.WithOptions(off), "segment", original);
        Check(!lean.ContainsKey("phonetic") && !lean.ContainsKey("word_forms") && !lean.ContainsKey("generated_example") && !lean.ContainsKey("collocations"), "disabled extras are not accepted");
        var prompt = Prompts.Word(template, "segment", original);
        Check(prompt[0].Content.Contains("不限于原句中的词性") && prompt.Count == 2 && prompt[1].Content.Contains("segment"), "broader senses requested for current selection only");
        Check(template.Render(new WordEntry { Values = values }).Contains("n. 段、部分"), "extra senses reach deterministic Markdown");
        Check(template.Fields.FindIndex(f => f.Key == "phonetic") == template.Fields.FindIndex(f => f.Key == "lemma") + 1, "IPA editor follows lemma");
        string markdown = template.Render(new WordEntry { Values = values });
        Check(markdown.IndexOf("**原形**", StringComparison.Ordinal) < markdown.IndexOf("**音标**", StringComparison.Ordinal) && markdown.IndexOf("**音标**", StringComparison.Ordinal) < markdown.IndexOf("**词性**", StringComparison.Ordinal), "IPA Markdown follows lemma before POS");
        Check(markdown.Contains("**释义**") && !markdown.Contains("语境释义") && !markdown.Contains("其他常见词性"), "unified meaning label");
        Check(!JsonSerializer.Deserialize<VocabularyOptions>("{\"supplement_senses\":false}", AppConfig.Json)!.IncludeWordForms, "old off switch migrates");
        Check(JsonSerializer.Deserialize<VocabularyOptions>("{\"include_word_forms\":true,\"supplement_senses\":false}", AppConfig.Json)!.IncludeWordForms, "new switch takes precedence");
        Check(!JsonSerializer.Serialize(all, AppConfig.Json).Contains("supplement_senses"), "only new option key exported");
        var stale = Prompts.ParseWord("{\"meaning\":\"v. 分割\",\"other_meanings\":\"n. 段、部分\"}", template, "segment", original);
        Check(stale["meaning"] == "v. 分割\nn. 段、部分" && !stale.ContainsKey("other_meanings"), "stale split senses merge into meaning");
        var overflow = new WordEntry { Values = new() { ["word"] = "test", ["meaning"] = "测试", ["word_forms"] = "a [n.] 一\nb [n.] 二\nc [n.] 三\nd [n.] 四" } };
        bool rejected = false;
        try { new VocabularyStore().Save(Path.Combine(directory, "must-not-save.md"), template, overflow, false); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected && !File.Exists(Path.Combine(directory, "must-not-save.md")), "manual fourth form rejected before writing");
        var legacyTemplate = JsonSerializer.Deserialize<VocabularyTemplate>(JsonSerializer.Serialize(basic, AppConfig.Json), AppConfig.Json)!;
        legacyTemplate.Fields.Single(f => f.Key == "meaning").Label = "语境释义";
        legacyTemplate.EntryTemplate = legacyTemplate.EntryTemplate.Replace("**释义**", "**语境释义**");
        var upgraded = legacyTemplate.WithOptions(all);
        Check(upgraded.Fields.Single(f => f.Key == "meaning").Label == "释义" && !upgraded.EntryTemplate.Contains("语境释义") && legacyTemplate.EntryTemplate.Contains("语境释义"), "legacy template labels upgrade only in memory");
        Check(template.WithOptions(all).EntryTemplate == template.EntryTemplate, "layout is idempotent");
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
            Check(cfg.Vocabulary.IncludeWordForms && !cfg.Vocabulary.GenerateExamples, "cancel discards switch drafts");
        }
        using (var settings = new SettingsDialog(cfg, useCredentials: false))
        {
            settings.Show();
            var toggles = Children(settings).OfType<SlideSwitch>().ToArray();
            Check(toggles.Length == 4 && toggles.All(t => t.AccessibleRole == AccessibleRole.CheckButton && t.TabStop), "four keyboard accessible switches");
            Check(toggles.Single(t => t.Name == "includeWordForms").Text == UiText.T("补充常见变形（最多 3 个）"), "renamed form switch");
            foreach (var toggle in toggles) toggle.Checked = true;
            Children(settings).OfType<Button>().Single(b => b.Text == UiText.T("保存设置")).PerformClick();
            Check(settings.IsDisposed && cfg.Vocabulary.IncludePhonetics && cfg.Vocabulary.IncludeCollocations && AppConfig.Load().Vocabulary.GenerateExamples, "save persists switches");
        }
        using var dialog = new WordDialog(cfg, new VocabularyStore(), new NeverCalledClient(), "segment", "Algorithms segment images.", () => "");
        dialog.Show(); await Task.Delay(80);
        var editors = Children(dialog).OfType<TextBox>().Where(t => t.Name.StartsWith("wordField_")).ToDictionary(t => t.Name);
        Check(editors["wordField_lemma"].Top < editors["wordField_phonetic"].Top && editors["wordField_phonetic"].Top < editors["wordField_part_of_speech"].Top, "real editor IPA order");
        Check(!editors.ContainsKey("wordField_other_meanings") && Children(dialog).OfType<Label>().Any(l => l.Text == UiText.T("释义 *")), "real unified meaning editor");
        Check(Children(dialog).OfType<TextBox>().Any(t => t.Name == "wordField_word_forms") && Children(dialog).OfType<TextBox>().Any(t => t.Name == "wordField_generated_example"), "enabled fields appear in real editor");
        dialog.Close();
    }
    sealed class NeverCalledClient : ITextModelClient
    {
        public Task<ApiResult> CompleteAsync(ModelRequestOptions options, string key, IReadOnlyList<ChatMessage> messages, bool json, CancellationToken token) => throw new Exception("No request expected");
    }
    static IEnumerable<Control> Children(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (var child in Children(c)) yield return child; } }
}
