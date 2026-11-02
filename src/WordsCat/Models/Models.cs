using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReadDock;

public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 2;
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
        var c = File.Exists(AppPaths.ConfigFile)
            ? JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(AppPaths.ConfigFile), Json) ?? throw new InvalidDataException("settings.json 内容为空。")
            : new AppConfig();
        c.NormalizeNotebooks();
        if (!File.Exists(c.TemplateFile) && string.Equals(Path.GetFullPath(c.TemplateFile), Path.GetFullPath(Path.Combine(AppPaths.DataDirectory, "vocabulary.template.json")), StringComparison.OrdinalIgnoreCase))
            File.Copy(AppPaths.DefaultTemplate, c.TemplateFile);
        MigrateBundledTemplate(c.TemplateFile);
        c.SchemaVersion = 2;
        return c;
    }
    public void NormalizeNotebooks()
    {
        if (ModelProvider is not ("deepseek" or "custom"))
            ModelProvider = Uri.TryCreate(BaseUrl, UriKind.Absolute, out var providerUri) && ModelPresets.IsDeepSeek(providerUri) ? "deepseek" : "custom";
        if (RequestTimeoutSeconds is < 1 or > 600 || MaxRetries is < 0 or > 5)
            throw new InvalidDataException("模型请求超时须为 1—600 秒，重试次数须为 0—5。");
        UiLanguage = UiLanguage == "en" ? "en" : "zh-CN";
        TranslationDirection = "auto";
        if (TranslationInstruction == LegacyTranslationInstruction) TranslationInstruction = DefaultTranslationInstruction;
        HistoryLimit = Math.Clamp(HistoryLimit, 0, 1000);
        if (Notebooks.Count == 0) Notebooks.Add(new NotebookTarget { Name = Path.GetFileNameWithoutExtension(TargetFile), File = Path.GetFullPath(TargetFile) });
        if (Notebooks.Count > 40) throw new InvalidDataException("最多支持 40 个单词本。");
        foreach (var n in Notebooks)
        {
            if (string.IsNullOrWhiteSpace(n.Id)) n.Id = Guid.NewGuid().ToString("N");
            n.Name = n.Name.Trim();
            if (n.Name.Length == 0 || string.IsNullOrWhiteSpace(n.File)) throw new InvalidDataException("请填写单词本名称和 Markdown 路径。");
            n.File = Path.GetFullPath(n.File);
            if (!n.File.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("单词本路径必须以 .md 结尾。");
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
    public void Save() { NormalizeNotebooks(); AtomicFile.Write(AppPaths.ConfigFile, JsonSerializer.Serialize(this, Json)); }
}

public sealed class NotebookTarget
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string File { get; set; } = "";
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
