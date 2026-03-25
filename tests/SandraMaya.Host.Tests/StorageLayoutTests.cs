using SandraMaya.Host.Configuration;
using SandraMaya.Host.Storage;

namespace SandraMaya.Host.Tests;

public sealed class StorageLayoutTests
{
    [Fact]
    public void Create_ResolvesRuntimeSkillsPathFromContentRoot()
    {
        var contentRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"sandra-maya-host-{Guid.NewGuid():N}"));
        var options = new StorageOptions
        {
            Root = "App_Data",
            RuntimeSkillsPath = Path.Combine("Assistant", "Skills")
        };

        var layout = StorageLayout.Create(options, contentRoot);

        Assert.Equal(
            Path.Combine(contentRoot, "Assistant", "Skills"),
            layout.RuntimeSkillsPath);
        Assert.Equal(
            Path.Combine(contentRoot, "App_Data"),
            layout.Root);
    }
}
