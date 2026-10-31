using System.Text;
using System.Text.RegularExpressions;

namespace ReadDock;

// Decide from prose rather than LaTeX commands, variable names, links or numbers.
// Han characters and English words are compared as language units, so long
// technical English terms do not outweigh the Chinese sentence around them.
public static class TranslationLanguage
{
    static readonly Regex Math = new(@"(?s)\$\$.*?\$\$|(?<!\\)\$[^$\r\n]*\$|\\\[.*?\\\]|\\\(.*?\\\)", RegexOptions.Compiled);
    static readonly Regex CodeAndLinks = new(@"(?s)```.*?```|`[^`\r\n]*`|https?://\S+|\\[A-Za-z]+", RegexOptions.Compiled);
    static readonly Regex EnglishWords = new(@"[A-Za-z]+(?:['’\-][A-Za-z]+)*", RegexOptions.Compiled);
    public static string DetectDirection(string text)
    {
        var prose = CodeAndLinks.Replace(Math.Replace(text, " "), " ");
        int chinese = prose.EnumerateRunes().Count(r => r.Value is >= 0x3400 and <= 0x4DBF or >= 0x4E00 and <= 0x9FFF or >= 0xF900 and <= 0xFAFF or >= 0x20000 and <= 0x323AF);
        int english = EnglishWords.Matches(prose).Count(m => m.Value.Length > 1 || m.Value is "a" or "A" or "I");
        return chinese > 0 && chinese >= english ? "zh-en" : "en-zh";
    }
}
