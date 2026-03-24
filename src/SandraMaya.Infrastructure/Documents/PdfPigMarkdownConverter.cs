using System.Text;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Infrastructure.Helpers;
using UglyToad.PdfPig;

namespace SandraMaya.Infrastructure.Documents;

public sealed class PdfPigMarkdownConverter : IPdfToMarkdownConverter
{
    public async Task<PdfToMarkdownResult> ConvertAsync(Stream pdfContent, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfContent);

        await using var bufferedStream = new MemoryStream();
        if (pdfContent.CanSeek)
        {
            pdfContent.Position = 0;
        }

        await pdfContent.CopyToAsync(bufferedStream, cancellationToken);
        bufferedStream.Position = 0;

        var title = Path.GetFileNameWithoutExtension(fileName);

        try
        {
            using var document = PdfDocument.Open(bufferedStream);
            var markdownSections = new List<string>();
            var plainTextSections = new List<string>();

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var normalizedPageText = MemoryTextNormalizer.NormalizeExtractedText(page.Text);
                plainTextSections.Add(normalizedPageText);
                markdownSections.Add($$"""
## Page {{page.Number}}

{{FormatAsMarkdown(normalizedPageText)}}
""");
            }

            var plainText = string.Join("\n\n", plainTextSections.Where(static section => !string.IsNullOrWhiteSpace(section)));
            var markdownBody = markdownSections.Count == 0
                ? "> No readable text could be extracted from this PDF."
                : string.Join("\n\n", markdownSections);

            var markdown = $$"""
# {{title}}

{{markdownBody}}
""";
            return new PdfToMarkdownResult(true, markdown, plainText);
        }
        catch (Exception exception)
        {
            var fallbackMarkdown = $$"""
# {{title}}

> PDF extraction failed: {{exception.Message}}
""";
            return new PdfToMarkdownResult(false, fallbackMarkdown, string.Empty, exception.Message);
        }
    }

    private static string FormatAsMarkdown(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "> No readable text extracted from this page.";
        }

        var paragraphs = text
            .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static paragraph => paragraph.Replace("\n", " ", StringComparison.Ordinal))
            .ToArray();

        var builder = new StringBuilder();
        foreach (var paragraph in paragraphs)
        {
            builder.AppendLine(paragraph);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }
}
