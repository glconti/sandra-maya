using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Domain;

namespace SandraMaya.Host.Assistant;

/// <summary>
/// Maps Telegram user identity to a persistent <see cref="UserProfile"/>.
/// Uses a deterministic UUID v5 derived from the Telegram user ID so the
/// same Telegram user always gets the same UserProfile.Id.
/// </summary>
public interface IUserResolutionService
{
    Task<Guid> ResolveUserIdAsync(UserReference sender, CancellationToken cancellationToken = default);
}

public sealed class UserResolutionService : IUserResolutionService
{
    // Namespace UUID for Sandra Maya user identity mapping.
    // All Telegram user IDs are hashed under this namespace to produce deterministic v5 UUIDs.
    private static readonly Guid Namespace = Guid.Parse("b3a1d2e0-5c4f-4a8b-9d1e-7f6c3b2a0e5d");

    private readonly IMemoryCommandService _memoryCommand;
    private readonly IMemoryQueryService _memoryQuery;
    private readonly ILogger<UserResolutionService> _logger;

    public UserResolutionService(
        IMemoryCommandService memoryCommand,
        IMemoryQueryService memoryQuery,
        ILogger<UserResolutionService> logger)
    {
        _memoryCommand = memoryCommand;
        _memoryQuery = memoryQuery;
        _logger = logger;
    }

    public async Task<Guid> ResolveUserIdAsync(UserReference sender, CancellationToken cancellationToken = default)
    {
        var userId = CreateUuidV5(Namespace, sender.Id);

        var existing = await _memoryQuery.GetUserAsync(userId, cancellationToken);
        if (existing is not null)
        {
            return userId;
        }

        _logger.LogInformation(
            "Creating new UserProfile for Telegram user {TelegramId} → {UserId}",
            sender.Id, userId);

        await _memoryCommand.SaveUserAsync(new UserProfile
        {
            Id = userId,
            ExternalUserKey = sender.Id,
            DisplayName = sender.DisplayName ?? sender.Username ?? "User",
            PreferredLocale = "de-CH"
        }, cancellationToken);

        return userId;
    }

    /// <summary>
    /// RFC 4122 UUID v5 (SHA-1 based) — deterministic UUID from namespace + name.
    /// </summary>
    private static Guid CreateUuidV5(Guid namespaceId, string name)
    {
        var namespaceBytes = namespaceId.ToByteArray();
        SwapGuidByteOrder(namespaceBytes);

        var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        var buffer = new byte[namespaceBytes.Length + nameBytes.Length];
        Buffer.BlockCopy(namespaceBytes, 0, buffer, 0, namespaceBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, buffer, namespaceBytes.Length, nameBytes.Length);

        var hash = System.Security.Cryptography.SHA1.HashData(buffer);

        hash[6] = (byte)((hash[6] & 0x0F) | 0x50); // Version 5
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // Variant RFC 4122

        var result = new byte[16];
        Array.Copy(hash, 0, result, 0, 16);
        SwapGuidByteOrder(result);
        return new Guid(result);
    }

    private static void SwapGuidByteOrder(byte[] guid)
    {
        (guid[0], guid[3]) = (guid[3], guid[0]);
        (guid[1], guid[2]) = (guid[2], guid[1]);
        (guid[4], guid[5]) = (guid[5], guid[4]);
        (guid[6], guid[7]) = (guid[7], guid[6]);
    }
}
