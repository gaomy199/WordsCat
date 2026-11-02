namespace ReadDock;

public sealed class VocabularyOptions
{
    public bool SupplementSenses { get; set; } = true;
    public bool IncludePhonetics { get; set; }
    public bool GenerateExamples { get; set; }
    public bool IncludeCollocations { get; set; }

    internal IEnumerable<FieldSpec> Fields()
    {
        if (SupplementSenses) yield return Field("other_meanings", "其他常见词性与释义", "按词典原形补充常见词性及中文释义，按 n. / v. 等分组；不限于当前语境，避免生僻义，不重复语境释义", true);
        if (IncludePhonetics) yield return Field("phonetic", "音标", "词典原形的国际音标 IPA；英美不同可分别标明，不确定则留空", false);
        if (GenerateExamples)
        {
            yield return Field("generated_example", "AI 例句", "为该词生成一句自然简洁的英文例句；这是 AI 生成内容，不是文献原句", true);
            yield return Field("generated_example_translation", "AI 例句译文", "AI 例句对应的中文译文；AI 例句为空时留空", true);
        }
        if (IncludeCollocations) yield return Field("collocations", "常见搭配", "2—4 个确实常见的英文搭配及中文释义，例如 a series of — 一系列；没有可靠搭配则留空", true);
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
