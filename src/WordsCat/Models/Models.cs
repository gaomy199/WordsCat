using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReadDock;

public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 2;
    public VocabularyOptions Vocabulary { get; set; } = new();
    public string ModelProvider { get; set; } = "";
    public bool ServiceGuideSeen { get; set; }
    public string BaseUrl { get; set; } = ModelPresets.BaseUrl;
    public string Model { get; set; } = ModelPresets.Model;
    public string VisionModel { get; set; } = ModelPresets.Model;
    public int RequestTimeoutSeconds { get; set; } = 100;
    public int MaxRetries { get; set; } = 0;
    public string ScreenshotEngine { get; set; } = "vision";
    public string TargetFile { get; set; } = Path.Combine(AppPaths.DataDirectory, "文献生词.md");
    public string TemplateFile { get; set; } = Path.Combine(AppPaths.DataDirectory, "vocabulary.template.json");
    public const string LegacyTranslationInstruction = "将英文文献准确翻译为简体中文，保留公式、数值、单位和引用标记。专业术语首次出现时可附英文。直接输出译文，不添加开场白。";
    public const string DefaultTranslationInstruction = "准确翻译文献，保留公式、数值、单位和引用标记。专业术语首次出现时可附原文。直接输出译文，不添加开场白。";
    public string TranslationInstruction { get; set; } = DefaultTranslationInstruction;
    public string UiLanguage { get; set; } = "zh-CN";
    // Retained in JSON for compatibility; manual choices from older versions migrate to auto.
    public string TranslationDirection { get; set; } = "auto";
    public string Glossary { get; set; } = "";
    public int FloatingX { get; set; } = -1;
    public int FloatingY { get; set; } = -1;
    public bool ShowFloating { get; set; } = true;
    public bool ShowOnStartup { get; set; } = true;
    public bool SidebarCollapsed { get; set; }
    public List<NotebookTarget> Notebooks { get; set; } = [];
    public string DefaultNotebookId { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore] public string ActiveNotebookId { get; set; } = "";
    public string ThemeMode { get; set; } = "light";
    public string AccentColor { get; set; } = "#147265";
    public int BackgroundBrightness { get; set; } = 50;
    public string LogoFile { get; set; } = "";
    public int HistoryLimit { get; set; } = 100;
    [System.Text.Json.Serialization.JsonIgnore] public NotebookTarget ActiveNotebook =>
        Notebooks.FirstOrDefault(n => n.Id == ActiveNotebookId) ?? Notebooks.First(n => n.Id == DefaultNotebookId);

    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Converters = { new SecretStringConverter() } };

    public static AppConfig Load()
    {
        bool hasConfig = File.Exists(AppPaths.ConfigFile);
        var c = hasConfig
            ? JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(AppPaths.ConfigFile), Json) ?? throw new InvalidDataException("settings.json 内容为空。")
            : new AppConfig();
        // Preserve legacy target/default choices before merging copied notebooks.
        // A fresh directory with existing notebooks should not gain a phantom default.
        if (hasConfig) c.NormalizeNotebooks();
        c.DiscoverDataNotebooks();
        c.NormalizeNotebooks();
        if (!File.Exists(c.TemplateFile) && string.Equals(Path.GetFullPath(c.TemplateFile), Path.GetFullPath(Path.Combine(AppPaths.DataDirectory, "vocabulary.template.json")), StringComparison.OrdinalIgnoreCase))
            File.Copy(AppPaths.DefaultTemplate, c.TemplateFile);
        MigrateBundledTemplate(c.TemplateFile);
        c.SchemaVersion = 2;
        return c;
    }
    private void DiscoverDataNotebooks()
    {
        if (!Directory.Exists(AppPaths.DataDirectory) || Notebooks.Count >= 40) return;
        string[] files;
        try
        {
            // Only direct Markdown children: do not import backups, caches or subfolders.
            // Read filenames only; discovering a notebook never parses or changes its content.
            files = Directory.GetFiles(AppPaths.DataDirectory, "*", SearchOption.TopDirectoryOnly)
                .Where(file => file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
        var paths = Notebooks.Select(n => PortablePaths.Resolve(n.File)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = Notebooks.Select(n => n.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            if (Notebooks.Count >= 40) break;
            string path = Path.GetFullPath(file);
            if (!paths.Add(path)) continue;
            string stem = Path.GetFileNameWithoutExtension(path), name = stem;
            if (string.IsNullOrWhiteSpace(stem)) continue;
            int suffix = 2;
            while (!names.Add(name)) name = stem + " (" + suffix++ + ")";
            Notebooks.Add(new NotebookTarget { Name = name, File = path });
        }
    }
    public void NormalizeNotebooks()
    {
        Vocabulary ??= new();
        TemplateFile = PortablePaths.Resolve(TemplateFile);
        if (!string.IsNullOrWhiteSpace(LogoFile)) LogoFile = PortablePaths.Resolve(LogoFile);
        BaseUrl = ModelPresets.NormalizeBaseUrl(BaseUrl);
        if (ModelProvider is not ("deepseek" or "gitee" or "custom"))
            ModelProvider = Uri.TryCreate(BaseUrl, UriKind.Absolute, out var providerUri)
                ? ModelPresets.IsDeepSeek(providerUri) ? "deepseek" : ModelPresets.IsGitee(providerUri) ? "gitee" : "custom" : "custom";
        if (RequestTimeoutSeconds is < 1 or > 600 || MaxRetries is < 0 or > 5)
            throw new InvalidDataException("模型请求超时须为 1—600 秒，重试次数须为 0—5。");
        UiLanguage = UiLanguage == "en" ? "en" : "zh-CN";
        TranslationDirection = "auto";
        if (TranslationInstruction == LegacyTranslationInstruction) TranslationInstruction = DefaultTranslationInstruction;
        HistoryLimit = Math.Clamp(HistoryLimit, 0, 1000);
        if (Notebooks.Count == 0) Notebooks.Add(new NotebookTarget { Name = Path.GetFileNameWithoutExtension(TargetFile), File = PortablePaths.Resolve(TargetFile) });
        if (Notebooks.Count > 40) throw new InvalidDataException("最多支持 40 个单词本。");
        foreach (var n in Notebooks)
        {
            if (string.IsNullOrWhiteSpace(n.Id)) n.Id = Guid.NewGuid().ToString("N");
            n.Name = n.Name.Trim();
            if (n.Name.Length == 0 || string.IsNullOrWhiteSpace(n.File)) throw new InvalidDataException("请填写单词本名称和 Markdown 路径。");
            n.File = PortablePaths.Resolve(n.File);
            if (!n.File.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("单词本路径必须以 .md 结尾。");
            if (NotebookTarget.IsAutomaticName(n.Name))
            {
                string stem = Path.GetFileNameWithoutExtension(n.File), name = stem;
                int suffix = 2;
                while (Notebooks.Any(other => !ReferenceEquals(n, other) && string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase))) name = stem + " (" + suffix++ + ")";
                n.Name = name;
            }
        }
        if (Notebooks.Select(n => n.Id).Distinct().Count() != Notebooks.Count ||
            Notebooks.Select(n => n.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Notebooks.Count ||
            Notebooks.Select(n => n.File).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Notebooks.Count)
            throw new InvalidDataException("单词本的名称、路径和标识不能重复。");
        if (!Notebooks.Any(n => n.Id == DefaultNotebookId)) DefaultNotebookId = Notebooks[0].Id;
        if (!Notebooks.Any(n => n.Id == ActiveNotebookId)) ActiveNotebookId = DefaultNotebookId;
        TargetFile = Notebooks.First(n => n.Id == DefaultNotebookId).File;
    }
    public static bool MigrateBundledTemplate(string file)
    {
        const string oldHash = "2958D12F12799DEEFA646FE3F7ADF1D3AC0098B60FDACD2D3530437D26A64121";
        if (!File.Exists(file) || string.Equals(Path.GetFullPath(file), Path.GetFullPath(AppPaths.DefaultTemplate), StringComparison.OrdinalIgnoreCase)) return false;
        var bytes = File.ReadAllBytes(file);
        if (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) != oldHash) return false;
        var backup = file + ".v0.1.backup";
        if (!File.Exists(backup)) File.WriteAllBytes(backup, bytes);
        AtomicFile.Write(file, File.ReadAllText(AppPaths.DefaultTemplate));
        return true;
    }
    public void Save()
    {
        NormalizeNotebooks();
        // Keep absolute runtime paths; only the serialized copy contains portable paths.
        var stored = JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(this, Json), Json)!;
        stored.TemplateFile = PortablePaths.Store(TemplateFile);
        stored.TargetFile = PortablePaths.Store(TargetFile);
        stored.LogoFile = string.IsNullOrWhiteSpace(LogoFile) ? "" : PortablePaths.Store(LogoFile);
        foreach (var n in stored.Notebooks) n.File = PortablePaths.Store(n.File);
        AtomicFile.Write(AppPaths.ConfigFile, JsonSerializer.Serialize(stored, Json));
    }
}

public sealed class NotebookTarget
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string File { get; set; } = "";
    internal static bool IsAutomaticName(string name) => Regex.IsMatch(name.Trim(), @"^(?:单词本|Notebook)\s*\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public override string ToString() => Name;
    public NotebookTarget Copy() => new() { Id = Id, Name = Name, File = File };
}

public sealed class FieldSpec
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Required { get; set; }
    public bool Multiline { get; set; }
}

public sealed class VocabularyTemplate
{
    public int Version { get; set; } = 1;
    public string FileHeader { get; set; } = "# 文献生词本\n\n";
    public string EntryTemplate { get; set; } = "";
    public List<FieldSpec> Fields { get; set; } = [];
    public static VocabularyTemplate Load(string file)
    {
        var t = JsonSerializer.Deserialize<VocabularyTemplate>(File.ReadAllText(file), AppConfig.Json)
            ?? throw new InvalidDataException("模板不能为空。");
        if (t.Version != 1) throw new InvalidDataException("模板 version 必须为 1。");
        if (t.Fields.Count is < 1 or > 30 || t.Fields.Select(f => f.Key).Distinct().Count() != t.Fields.Count)
            throw new InvalidDataException("模板需要 1—30 个字段，字段 key 不能重复。");
        foreach (var f in t.Fields)
        {
            if (!Regex.IsMatch(f.Key, @"^[a-z][a-z0-9_]{0,49}$") || f.Key is "id" or "date" or "captured_at")
                throw new InvalidDataException("字段 key 请使用英文小写和下划线，且不要使用 id/date/captured_at：" + f.Key);
        }
        if (!t.Fields.Any(f => f.Key == "word" && f.Required)) throw new InvalidDataException("请保留必填的 word 字段。");
        if (string.IsNullOrWhiteSpace(t.EntryTemplate) || !t.EntryTemplate.Contains("{{word}}")) throw new InvalidDataException("entry_template 必须包含 {{word}}。");
        var allowed = t.Fields.Select(f => f.Key).Concat(["id", "date", "captured_at"]).ToHashSet();
        foreach (Match m in Regex.Matches(t.EntryTemplate, @"\{\{([a-z0-9_]+)\}\}"))
            if (!allowed.Contains(m.Groups[1].Value)) throw new InvalidDataException("模板引用了未定义的字段：" + m.Groups[1].Value);
        return t;
    }
    public VocabularyTemplate WithOptions(VocabularyOptions options)
    {
        var copy = JsonSerializer.Deserialize<VocabularyTemplate>(JsonSerializer.Serialize(this, AppConfig.Json), AppConfig.Json)!;
        var meaning = copy.Fields.FirstOrDefault(f => f.Key == "meaning");
        if (meaning?.Label == "语境释义") meaning.Label = "释义";
        if (meaning != null && meaning.Description == "结合文献原句的简明中文释义")
            meaning.Description = "简明中文释义：当前语境义在前，再按词性补充该词的其他常见含义；避免生僻义";
        copy.EntryTemplate = copy.EntryTemplate.Replace("**语境释义**", "**释义**");
        // Upgrade reserved optional fields in memory, preserving the user's template file.
        var oldSenses = copy.Fields.FirstOrDefault(f => f.Key == "other_meanings");
        if (oldSenses != null)
        {
            copy.Fields.Remove(oldSenses);
            var forms = new VocabularyOptions().Fields().Single(f => f.Key == "word_forms");
            if (!copy.Fields.Any(f => f.Key == forms.Key)) copy.Fields.Add(forms);
            copy.EntryTemplate = copy.EntryTemplate.Replace("{{other_meanings}}", "{{word_forms}}")
                .Replace("其他常见词性与释义", "常见变形");
        }
        foreach (var field in options.Fields())
        {
            if (!copy.Fields.Any(f => f.Key == field.Key)) copy.Fields.Add(field);
            if (!copy.EntryTemplate.Contains("{{" + field.Key + "}}", StringComparison.Ordinal))
                copy.AppendFieldLine(field);
        }
        var phonetic = copy.Fields.FirstOrDefault(f => f.Key == "phonetic");
        if (phonetic != null)
        {
            copy.Fields.Remove(phonetic);
            int lemma = copy.Fields.FindIndex(f => f.Key == "lemma");
            copy.Fields.Insert(lemma >= 0 ? lemma + 1 : Math.Min(1, copy.Fields.Count), phonetic);
            // Only move a complete standard field line; retain inline custom placeholders.
            string pattern = @"(?m)^[ \t]*-[ \t]*\*\*" + Regex.Escape(phonetic.Label) + @"\*\*[:：][ \t]*\{\{phonetic\}\}[ \t]*\r?$(?:\n)?";
            if (Regex.IsMatch(copy.EntryTemplate, pattern))
            {
                copy.EntryTemplate = Regex.Replace(copy.EntryTemplate, pattern, "");
                var lemmaLine = Regex.Match(copy.EntryTemplate, @"(?m)^.*\{\{lemma\}\}[^\r\n]*(?:\r?\n|$)");
                if (lemmaLine.Success)
                {
                    int position = lemmaLine.Index + lemmaLine.Length;
                    string prefix = lemmaLine.Value.EndsWith('\n') ? "" : "\n";
                    copy.EntryTemplate = copy.EntryTemplate.Insert(position, prefix + FieldLine(phonetic));
                }
                else copy.AppendFieldLine(phonetic);
            }
        }
        return copy;
    }
    private static string FieldLine(FieldSpec field) => "- **" + field.Label + "**：{{" + field.Key + "}}\n";
    private void AppendFieldLine(FieldSpec field)
    {
        int separator = EntryTemplate.LastIndexOf("\n---", StringComparison.Ordinal);
        EntryTemplate = separator >= 0
            ? EntryTemplate[..separator].TrimEnd() + "\n" + FieldLine(field) + EntryTemplate[separator..]
            : EntryTemplate.TrimEnd() + "\n" + FieldLine(field);
    }
    public string Render(WordEntry entry)
    {
        var values = new Dictionary<string, string>(entry.Values)
        {
            ["id"] = entry.Id,
            ["date"] = entry.CreatedAt.ToString("yyyy-MM-dd"),
            ["captured_at"] = entry.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz")
        };
        return Regex.Replace(EntryTemplate, @"\{\{([a-z0-9_]+)\}\}", m => MarkdownValue(values.GetValueOrDefault(m.Groups[1].Value, ""))).TrimEnd() + "\n";
    }
    private static string MarkdownValue(string value)
    {
        value = value.Replace("\r\n", "\n").Replace("\r", "\n");
        var result = new StringBuilder(); int start = 0;
        foreach (Match math in Regex.Matches(value, @"(?<!\\)\$(?!\$)[^$\n]+\$"))
        {
            result.Append(EscapePlain(value[start..math.Index]));
            // Keep LaTeX variables intact for Obsidian's math renderer.
            result.Append(math.Value.Replace("<", @"\lt ").Replace(">", @"\gt "));
            start = math.Index + math.Length;
        }
        result.Append(EscapePlain(value[start..])); return result.ToString();
    }
    private static string EscapePlain(string value) => value
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
        .Replace("\\", "\\\\").Replace("`", "\\`").Replace("*", "\\*").Replace("_", "\\_")
        .Replace("[", "\\[").Replace("]", "\\]").Replace("#", "\\#").Replace("|", "\\|")
        .Replace("\n", "<br>");
}

public sealed class WordEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public Dictionary<string, string> Values { get; set; } = [];
    public string NormalizedWord => Normalize(Values.GetValueOrDefault("word", ""));
    public static string Normalize(string s) => Regex.Replace(s.Trim().Normalize(NormalizationForm.FormKC), @"\s+", " ").ToLowerInvariant();
}

public static class AtomicFile
{
    public static void Write(string file, string text)
    {
        var path = Path.GetFullPath(file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(tmp, text, new UTF8Encoding(false)); File.Move(tmp, path, true); }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
