using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;

namespace SandraMaya.Infrastructure.Tests;

public sealed class LocalFileStorageTests
{
    [Fact]
    public async Task SaveAsync_WritesFileAndHash()
    {
        await using var harness = await InfrastructureTestHarness.CreateAsync();
        await using var scope = harness.Services.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("hello from sandra maya"));
        var descriptor = await storage.SaveAsync(
            new FileStorageWriteRequest("hello.txt", "text/plain", StoragePartition.Uploads, content));

        Assert.True(File.Exists(descriptor.AbsolutePath));
        Assert.Equal("text/plain", descriptor.ContentType);
        Assert.Equal(new FileInfo(descriptor.AbsolutePath).Length, descriptor.ByteCount);

        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("hello from sandra maya"))).ToLowerInvariant();
        Assert.Equal(expectedHash, descriptor.Sha256);
    }
}
