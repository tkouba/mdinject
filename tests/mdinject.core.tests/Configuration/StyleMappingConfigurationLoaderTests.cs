using Mdinject.Core.Configuration;

namespace Mdinject.Core.Tests.Configuration;

public sealed class StyleMappingConfigurationLoaderTests : IDisposable
{
    private readonly string configPath;
    private readonly StyleMappingConfigurationLoader loader = new();

    public StyleMappingConfigurationLoaderTests()
    {
        configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.yaml");
    }

    [Fact]
    public async Task LoadAsync_ParsesConfiguredBlockAndInlineKeys()
    {
        await WriteConfigAsync(
            """
            blocks:
              heading1: Heading 1
              paragraph: Normal
            inlines:
              bold:
              italic:
              code: "Inline Code"
            """);

        var configuration = await loader.LoadAsync(configPath);

        Assert.Equal("Heading 1", configuration.Blocks[BlockStyleKey.Heading1]);
        Assert.Equal("Normal", configuration.Blocks[BlockStyleKey.Paragraph]);
        Assert.Equal("Inline Code", configuration.Inlines[InlineStyleKey.Code]);

        // A blank inline value is kept as an explicit null, distinct from the key being absent.
        Assert.True(configuration.Inlines.ContainsKey(InlineStyleKey.Bold));
        Assert.Null(configuration.Inlines[InlineStyleKey.Bold]);
        Assert.True(configuration.Inlines.ContainsKey(InlineStyleKey.Italic));
        Assert.Null(configuration.Inlines[InlineStyleKey.Italic]);
    }

    [Fact]
    public async Task LoadAsync_WithMissingSection_ReturnsEmptyMap()
    {
        await WriteConfigAsync(
            """
            blocks:
              paragraph: Normal
            """);

        var configuration = await loader.LoadAsync(configPath);

        Assert.Single(configuration.Blocks);
        Assert.Empty(configuration.Inlines);
    }

    [Fact]
    public async Task LoadAsync_WithUnknownBlockKey_ThrowsStyleMappingConfigurationException()
    {
        await WriteConfigAsync(
            """
            blocks:
              heading10: Heading 10
            """);

        await Assert.ThrowsAsync<StyleMappingConfigurationException>(() => loader.LoadAsync(configPath));
    }

    [Fact]
    public async Task LoadAsync_WithInvalidYaml_ThrowsStyleMappingConfigurationException()
    {
        await WriteConfigAsync(
            """
            blocks: [this is not a mapping
            """);

        await Assert.ThrowsAsync<StyleMappingConfigurationException>(() => loader.LoadAsync(configPath));
    }

    [Fact]
    public async Task LoadAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        await WriteConfigAsync(
            """
            blocks:
              paragraph: Normal
            """);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loader.LoadAsync(configPath, cts.Token));
    }

    public void Dispose()
    {
        if (File.Exists(configPath))
            File.Delete(configPath);
    }

    private Task WriteConfigAsync(string yaml)
    {
        return File.WriteAllTextAsync(configPath, yaml);
    }
}
