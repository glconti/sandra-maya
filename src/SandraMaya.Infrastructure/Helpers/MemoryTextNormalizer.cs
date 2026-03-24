using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SandraMaya.Application.Domain;

namespace SandraMaya.Infrastructure.Helpers;

internal static partial class MemoryTextNormalizer
{
    private static readonly Regex MarkdownTokensRegex = MarkdownTokens();
    private static readonly Regex WhitespaceRegex = Whitespace();
    private static readonly Regex SearchTokenRegex = SearchToken();
    private static readonly Regex NonSpacingMarksRegex = NonSpacingMarks();

    public static string NormalizeExtractedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n')
            .Select(static line => WhitespaceRegex.Replace(line.Trim(), " "));

        return string.Join("\n", lines).Trim();
    }

    public static string MarkdownToPlainText(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var withoutTokens = MarkdownTokensRegex.Replace(markdown, " ");
        return WhitespaceRegex.Replace(withoutTokens, " ").Trim();
    }

    public static string NormalizeKeyPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        var cleaned = NonSpacingMarksRegex.Replace(builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant(), "-");
        return cleaned.Trim('-');
    }

    public static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string BuildJobPostingDedupeKey(JobPosting posting)
    {
        var identity = string.Join('|', new[]
        {
            NormalizeKeyPart(posting.SourceSite),
            NormalizeKeyPart(posting.SourcePostingId ?? string.Empty),
            NormalizeKeyPart(posting.Title),
            NormalizeKeyPart(posting.CompanyName),
            NormalizeKeyPart(posting.Location)
        });

        return ComputeSha256(identity);
    }

    public static string BuildFtsQuery(string query)
    {
        var tokens = GetSearchTokens(query);

        return tokens.Length == 0
            ? string.Empty
            : string.Join(" AND ", tokens.Select(static token => $"{token}*"));
    }

    public static string[] GetSearchTokens(string query) =>
        SearchTokenRegex.Matches(query)
            .Select(static match => match.Value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    [GeneratedRegex(@"[`#>*_\[\]()~-]+", RegexOptions.Compiled)]
    private static partial Regex MarkdownTokens();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.Compiled)]
    private static partial Regex SearchToken();

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonSpacingMarks();
}
