namespace ReadDock;

public sealed class VocabularyOptions
{
    private bool? includeWordForms, legacySupplementSenses;
    public bool IncludeWordForms { get => includeWordForms ?? legacySupplementSenses ?? true; set => includeWordForms = value; }
    // Read v0.3.8's switch without continuing to export its old meaning or key.
    [System.Text.Json.Serialization.JsonPropertyName("supplement_senses")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? LegacySupplementSenses { get => null; set => legacySupplementSenses = value; }
    public bool IncludePhonetics { get; set; }
    public bool GenerateExamples { get; set; }
    public bool IncludeCollocations { get; set; }

    internal IEnumerable<FieldSpec> Fields()
    {
        if (IncludeWordForms) yield return Field("word_forms", "常见变形", "同词族常见派生词或屈折变化，最多 3 个，每行一个：英文词形 [词性] 中文释义；优先跨词性，不重复所选词，不填该词的其他中文含义，不确定则留空", true);
        if (IncludePhonetics) yield return Field("phonetic", "音标", "词典原形的国际音标 IPA；英美不同可分别标明，不确定则留空", false);
        if (GenerateExamples)
        {
            yield return Field("generated_example", "AI 例句", "为该词生成一句自然简洁的英文例句；这是 AI 生成内容，不是文献原句", true);
            yield return Field("generated_example_translation", "AI 例句译文", "AI 例句对应的中文译文；AI 例句为空时留空", true);
        }
        if (IncludeCollocations) yield return Field("collocations", "常见搭配", "2—4 个确实常见的英文搭配及中文释义，例如 a series of — 一系列；没有可靠搭配则留空", true);
    }

    internal static string[] WordFormLines(string text) => System.Text.RegularExpressions.Regex.Split(text.Trim(), @"\r\n?|\n|[；;](?=\s*[A-Za-z][^；;\r\n]*\[)")
        .Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    internal static string LimitWordForms(string text) => string.Join("\n", WordFormLines(text).Take(3));
    internal static void ValidateWordForms(WordEntry entry)
    {
        if (WordFormLines(entry.Values.GetValueOrDefault("word_forms", "")).Length > 3)
            throw new InvalidOperationException("常见变形最多填写 3 项，每行一个词形、词性和释义。");
    }

    private static FieldSpec Field(string key, string label, string description, bool multiline) =>
        new() { Key = key, Label = label, Description = description, Multiline = multiline };
}

internal static class PortablePaths
{
    // Relative paths always use settings.json's directory, never the process working directory.
    internal static string Resolve(string path) => Resolve(path, AppPaths.DataDirectory, AppContext.BaseDirectory);
    internal static string Store(string path) => Store(path, AppPaths.DataDirectory, AppContext.BaseDirectory);

    internal static string Resolve(string path, string dataDirectory, string applicationDirectory)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("请填写文件路径。");
        string full = Path.GetFullPath(path, Path.GetFullPath(dataDirectory));
        if (!Path.IsPathRooted(path) || File.Exists(full)) return full;
        // Recover a moved legacy Data path only when that exact suffix exists locally.
        string marker = Path.DirectorySeparatorChar + "Data" + Path.DirectorySeparatorChar;
        int index = full.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            string candidate = Path.GetFullPath(full[(index + marker.Length)..], Path.GetFullPath(dataDirectory));
            if (File.Exists(candidate)) return candidate;
        }
        return full;
    }

    internal static string Store(string path, string dataDirectory, string applicationDirectory)
    {
        string full = Path.GetFullPath(path, Path.GetFullPath(dataDirectory));
        bool Inside(string root) => full.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        return Inside(dataDirectory) || Inside(applicationDirectory) ? Path.GetRelativePath(dataDirectory, full) : full;
    }
}
