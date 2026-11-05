using System.Text;
using System.Text.RegularExpressions;

namespace ReadDock;

// Replace only locally identified entry ranges; never ask a model to rewrite a notebook.
internal static class NotebookReplacement
{
    readonly record struct Span(int Start, int End);
    internal static string Replace(string text, string word, string replacement)
    {
        var fenced = new List<Span>();
        var headings = new List<Match>();
        int fenceStart = -1, fenceLength = 0; char fenceChar = '\0';
        foreach (Match line in Regex.Matches(text, @"(?m)^[ \t]{0,3}(?:(?<fence>\x60{3,}|~{3,})(?<tail>[^\r\n]*)|(?<level>#{1,2})[ \t]+(?<heading>[^\r\n]*))\r?$"))
        {
            string fence = line.Groups["fence"].Value;
            if (fence.Length > 0)
            {
                if (fenceStart < 0) { fenceStart = line.Index; fenceLength = fence.Length; fenceChar = fence[0]; }
                else if (fence[0] == fenceChar && fence.Length >= fenceLength && string.IsNullOrWhiteSpace(line.Groups["tail"].Value))
                { fenced.Add(new(fenceStart, line.Index + line.Length)); fenceStart = -1; }
            }
            else if (fenceStart < 0) headings.Add(line);
        }
        if (fenceStart >= 0) fenced.Add(new(fenceStart, text.Length));
        bool InFence(int index) => fenced.Any(span => span.Start <= index && index < span.End);
        var starts = Regex.Matches(text, @"(?m)^<!-- readdock:(?<id>[A-Za-z0-9_-]+) word=(?<word>[A-Za-z0-9+/=]+) -->[ \t]*\r?$")
            .Cast<Match>().Where(match => !InFence(match.Index)).ToArray();
        string encodedWord = Convert.ToBase64String(Encoding.UTF8.GetBytes(WordEntry.Normalize(word)));
        var cards = new List<Span>(); var matches = new List<Span>();
        for (int index = 0; index < starts.Length; index++)
        {
            var start = starts[index];
            int next = index + 1 < starts.Length ? starts[index + 1].Index : text.Length;
            string closing = "<!-- /readdock:" + start.Groups["id"].Value + " -->";
            int end = text.IndexOf(closing, start.Index + start.Length, StringComparison.Ordinal);
            bool valid = end >= 0 && end + closing.Length <= next;
            bool sameWord = start.Groups["word"].Value == encodedWord;
            if (!valid && sameWord) throw new InvalidDataException("旧词条的保存标记不完整，无法安全覆盖。请先在单词本中修复该词条。");
            int after = valid ? end + closing.Length : next;
            if (valid && after < text.Length && text[after] == '\r') after++;
            if (valid && after < text.Length && text[after] == '\n') after++;
            var span = new Span(start.Index, after); cards.Add(span);
            if (sameWord) matches.Add(span);
        }
        bool InCard(int index) => cards.Any(span => span.Start <= index && index < span.End);
        // Support older unmarked ## word sections, while preserving other headings/cards.
        var outsideHeadings = headings.Where(heading => !InCard(heading.Index)).ToArray();
        for (int index = 0; index < outsideHeadings.Length; index++)
        {
            var heading = outsideHeadings[index];
            if (heading.Groups["level"].Value != "##" || WordEntry.Normalize(heading.Groups["heading"].Value) != WordEntry.Normalize(word)) continue;
            int end = index + 1 < outsideHeadings.Length ? outsideHeadings[index + 1].Index : text.Length;
            var nextCard = cards.FirstOrDefault(card => card.Start > heading.Index);
            if (nextCard.End > 0) end = Math.Min(end, nextCard.Start);
            matches.Add(new(heading.Index, end));
        }
        if (matches.Count == 0) throw new InvalidDataException("无法确定旧词条的完整范围，未覆盖文件。请先检查单词本中的词条格式。");
        if (text.Contains("\r\n", StringComparison.Ordinal)) replacement = replacement.Replace("\r\n", "\n").Replace("\n", "\r\n");
        var output = new StringBuilder(text.Length); int cursor = 0; bool inserted = false;
        foreach (var span in matches.OrderBy(span => span.Start))
        {
            if (span.Start < cursor) throw new InvalidDataException("旧词条范围重叠，未覆盖文件。请先检查单词本中的词条格式。");
            output.Append(text, cursor, span.Start - cursor);
            if (!inserted) { output.Append(replacement); inserted = true; }
            cursor = span.End;
        }
        output.Append(text, cursor, text.Length - cursor);
        return output.ToString();
    }
}
