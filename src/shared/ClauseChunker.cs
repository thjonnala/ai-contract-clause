using System.Text;
using System.Text.RegularExpressions;

namespace ContractClause.Shared;

/// <summary>One page of extracted contract text.</summary>
public record PageText(int PageNumber, string Text);

/// <summary>A clause-aware chunk produced by <see cref="ClauseChunker"/>.</summary>
public record ClauseChunk(string ClauseNumber, string ClauseTitle, string Text, int PageNumber);

/// <summary>
/// Splits page-tagged contract text on clause boundaries: numbered headings
/// (1., 1.1, 1.1.1), "Section 5" / "Article IV" headings, and ALL-CAPS headings.
/// Not fixed token windows — chunk boundaries always follow clause structure.
/// </summary>
public static partial class ClauseChunker
{
    private const string TitleGroup = "title";

    // "1." / "1.1" / "2)" followed by a title on the same line
    [GeneratedRegex(@"^\s*(?<num>\d+(?:\.\d+)*)[.)]?\s+(?<title>\S.*)$")]
    private static partial Regex NumberedHeading();

    // a bare clause number alone on a line (title continues on the next line/page)
    [GeneratedRegex(@"^\s*(?<num>\d+(?:\.\d+)*)[.)]?\s*$")]
    private static partial Regex BareNumber();

    // "Section 5 - Termination" / "ARTICLE IV: Term" (arabic or roman numerals)
    [GeneratedRegex(@"^\s*(?:Section|Article|SECTION|ARTICLE)\s+(?<num>\d+(?:\.\d+)*|[IVXLCDM]+)\b[\s.:\-–—]*(?<title>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex SectionHeading();

    // standalone ALL-CAPS heading line, e.g. "CONFIDENTIALITY"
    [GeneratedRegex(@"^\s*(?<title>[A-Z][A-Z0-9 ,&/'\-]{3,60})\s*$")]
    private static partial Regex AllCapsHeading();

    public static List<ClauseChunk> Chunk(IEnumerable<PageText> pages)
    {
        var lines = FlattenToLines(pages);

        var chunks = new List<ClauseChunk>();
        string curNumber = "";
        string curTitle = "Preamble";
        int curPage = lines.Count > 0 ? lines[0].Page : 1;
        var body = new StringBuilder();

        void Flush()
        {
            var text = body.ToString().Trim();
            if (text.Length > 0 || curNumber.Length > 0)
                chunks.Add(new ClauseChunk(curNumber, curTitle, text, curPage));
            body.Clear();
        }

        int i = 0;
        while (i < lines.Count)
        {
            var (line, page) = lines[i];
            int next = i + 1; // heading matchers that consume a lookahead line advance this

            if (string.IsNullOrWhiteSpace(line))
            {
                body.AppendLine();
            }
            // section headings and bare numbers may consume a lookahead line;
            // their patterns are mutually exclusive with NumberedHeading, so
            // sharing one branch preserves the original match order
            else if (TryMatchSectionHeading(lines, i, line, out var number, out var title, out var lastConsumed)
                || TryMatchBareNumber(lines, i, line, out number, out title, out lastConsumed))
            {
                Flush();
                curNumber = number;
                curTitle = title;
                curPage = page;
                next = lastConsumed + 1;
            }
            else if (TryMatchNumberedHeading(line, out number, out title))
            {
                Flush();
                curNumber = number;
                curTitle = title;
                curPage = page;
            }
            else if (TryMatchAllCapsHeading(line, out title))
            {
                Flush();
                curNumber = "";
                curTitle = title;
                curPage = page;
            }
            else
            {
                body.AppendLine(line);
            }

            i = next;
        }
        Flush();
        return chunks;
    }

    // flatten to (line, pageNumber) preserving order so headings split
    // across page breaks are handled uniformly
    private static List<(string Line, int Page)> FlattenToLines(IEnumerable<PageText> pages)
    {
        var lines = new List<(string Line, int Page)>();
        foreach (var page in pages)
        {
            foreach (var raw in page.Text.Replace("\r\n", "\n").Split('\n'))
                lines.Add((raw.TrimEnd(), page.PageNumber));
        }
        return lines;
    }

    // "Section 5 - Termination"; a title-less heading pulls its title from the next non-empty line
    private static bool TryMatchSectionHeading(
        List<(string Line, int Page)> lines, int index, string line,
        out string number, out string title, out int lastConsumed)
    {
        var m = SectionHeading().Match(line);
        if (!m.Success)
        {
            number = ""; title = ""; lastConsumed = index;
            return false;
        }

        number = m.Groups["num"].Value;
        title = m.Groups[TitleGroup].Value.Trim();
        lastConsumed = index;
        if (title.Length == 0 && TryNextNonEmpty(lines, index, out var t, out var consumed))
        {
            title = t.Trim();
            lastConsumed = consumed;
        }
        return true;
    }

    // "1. Definitions" — number and title on the same line
    private static bool TryMatchNumberedHeading(string line, out string number, out string title)
    {
        var m = NumberedHeading().Match(line);
        if (!m.Success || !LooksLikeTitle(m.Groups[TitleGroup].Value))
        {
            number = ""; title = "";
            return false;
        }

        number = m.Groups["num"].Value;
        title = m.Groups[TitleGroup].Value.Trim().TrimEnd('.');
        return true;
    }

    // a bare clause number ("7.1") whose title is the next non-empty line, possibly on the next page
    private static bool TryMatchBareNumber(
        List<(string Line, int Page)> lines, int index, string line,
        out string number, out string title, out int lastConsumed)
    {
        var m = BareNumber().Match(line);
        if (m.Success && TryNextNonEmpty(lines, index, out var t, out var idx) && LooksLikeTitle(t))
        {
            number = m.Groups["num"].Value;
            title = t.Trim().TrimEnd('.');
            lastConsumed = idx;
            return true;
        }

        number = ""; title = ""; lastConsumed = index;
        return false;
    }

    // standalone ALL-CAPS heading, e.g. "CONFIDENTIALITY"
    private static bool TryMatchAllCapsHeading(string line, out string title)
    {
        var m = AllCapsHeading().Match(line);
        if (!m.Success || CountLetters(line) < 4)
        {
            title = "";
            return false;
        }

        title = ToTitleCase(m.Groups[TitleGroup].Value.Trim());
        return true;
    }

    private static bool TryNextNonEmpty(List<(string Line, int Page)> lines, int from, out string next, out int index)
    {
        for (int j = from + 1; j < lines.Count; j++)
        {
            if (!string.IsNullOrWhiteSpace(lines[j].Line)) { next = lines[j].Line; index = j; return true; }
        }
        next = ""; index = from;
        return false;
    }

    // Heading titles are short; sentences of body text that happen to start
    // with a number (e.g. "30 days after...") are not titles.
    private static bool LooksLikeTitle(string s)
    {
        s = s.Trim();
        if (s.Length == 0 || s.Length > 80) return false;
        if (s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 10) return false;
        return char.IsUpper(s[0]);
    }

    private static int CountLetters(string s) => s.Count(char.IsLetter);

    private static string ToTitleCase(string s)
    {
        var ti = System.Globalization.CultureInfo.InvariantCulture.TextInfo;
        return ti.ToTitleCase(s.ToLowerInvariant());
    }
}
