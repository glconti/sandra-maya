using System.Text.RegularExpressions;
using Markdig;

namespace SandraMaya.Host.Telegram;

// Converts standard markdown (as produced by Azure OpenAI) to the HTML subset
// supported by Telegram's sendMessage parse_mode=HTML.
//
// Telegram supports: <b>, <i>, <u>, <s>, <code>, <pre>, <a href>, <blockquote>
// Everything else is either mapped to a supported equivalent or stripped.
internal static partial class TelegramMarkdownToHtmlConverter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseEmphasisExtras()
        .Build();

    private static readonly HashSet<string> SupportedTags = new(StringComparer.OrdinalIgnoreCase)
        { "b", "i", "u", "s", "code", "pre", "a", "blockquote" };

    public static string Convert(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return markdown;

        var html = Markdown.ToHtml(markdown, Pipeline);

        // Normalize semantic equivalents to Telegram-supported tags
        html = html.Replace("<strong>", "<b>", StringComparison.Ordinal)
                   .Replace("</strong>", "</b>", StringComparison.Ordinal)
                   .Replace("<em>", "<i>", StringComparison.Ordinal)
                   .Replace("</em>", "</i>", StringComparison.Ordinal)
                   .Replace("<del>", "<s>", StringComparison.Ordinal)
                   .Replace("</del>", "</s>", StringComparison.Ordinal)
                   .Replace("<strike>", "<s>", StringComparison.Ordinal)
                   .Replace("</strike>", "</s>", StringComparison.Ordinal);

        // Headings → bold on its own line
        html = HeadingRegex().Replace(html, "\n<b>$1</b>\n");

        // List items → bullet points; strip any inner <p> tags the list item may contain
        html = ListItemRegex().Replace(html, m =>
        {
            var content = InnerParagraphRegex().Replace(m.Groups[1].Value, "").Trim();
            return $"• {content}\n";
        });

        // Strip list wrapper tags
        html = ListWrapperRegex().Replace(html, "");

        // Paragraphs → keep content with trailing blank line
        html = ParagraphRegex().Replace(html, "$1\n\n");

        // <br> → newline
        html = LineBreakRegex().Replace(html, "\n");

        // <hr> → remove
        html = HorizontalRuleRegex().Replace(html, "");

        // Strip any remaining unsupported HTML tags while preserving supported ones
        html = AnyTagRegex().Replace(html, m =>
            SupportedTags.Contains(m.Groups[2].Value) ? m.Value : string.Empty);

        // Collapse excessive blank lines
        html = ExcessiveNewlinesRegex().Replace(html, "\n\n");

        return html.Trim();
    }

    [GeneratedRegex(@"<h[1-6][^>]*>(.*?)</h[1-6]>", RegexOptions.Singleline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"<li[^>]*>(.*?)</li>", RegexOptions.Singleline)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"</?p>", RegexOptions.None)]
    private static partial Regex InnerParagraphRegex();

    [GeneratedRegex(@"</?[uo]l[^>]*>", RegexOptions.Singleline)]
    private static partial Regex ListWrapperRegex();

    [GeneratedRegex(@"<p>(.*?)</p>", RegexOptions.Singleline)]
    private static partial Regex ParagraphRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.None)]
    private static partial Regex LineBreakRegex();

    [GeneratedRegex(@"<hr\s*/?>", RegexOptions.None)]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex(@"<(/?)(\w+)([^>]*)>", RegexOptions.None)]
    private static partial Regex AnyTagRegex();

    [GeneratedRegex(@"\n{3,}", RegexOptions.None)]
    private static partial Regex ExcessiveNewlinesRegex();
}
