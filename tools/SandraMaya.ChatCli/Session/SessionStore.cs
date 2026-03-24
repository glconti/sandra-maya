using System.Text.Json;

namespace SandraMaya.ChatCli.Session;

public static class SessionStore
{
    private static readonly string SessionDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sandra-maya-chat");

    private static readonly string SessionFile = Path.Combine(SessionDir, "session.json");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static void Save(ChatSession session)
    {
        Directory.CreateDirectory(SessionDir);
        File.WriteAllText(SessionFile, JsonSerializer.Serialize(session, JsonOptions));
    }

    public static ChatSession? TryLoad()
    {
        if (!File.Exists(SessionFile))
            return null;

        try
        {
            var json = File.ReadAllText(SessionFile);
            return JsonSerializer.Deserialize<ChatSession>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void Delete()
    {
        if (File.Exists(SessionFile))
            File.Delete(SessionFile);
    }
}
