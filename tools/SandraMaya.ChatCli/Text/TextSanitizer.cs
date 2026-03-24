namespace SandraMaya.ChatCli.Text;

internal static class TextSanitizer
{
    public static string StripControlCharacters(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var buffer = new char[text.Length];
        var index = 0;

        foreach (var ch in text)
        {
            if (!char.IsControl(ch) || ch is '\r' or '\n' or '\t')
                buffer[index++] = ch;
        }

        return index == text.Length
            ? text
            : new string(buffer, 0, index);
    }
}
