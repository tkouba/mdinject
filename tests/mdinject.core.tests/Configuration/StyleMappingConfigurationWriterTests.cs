using Mdinject.Core.Configuration;

namespace Mdinject.Core.Tests.Configuration;

public sealed class StyleMappingConfigurationWriterTests : IDisposable
{
    private readonly string outputPath;
    private readonly StyleMappingConfigurationWriter writer = new();

    public StyleMappingConfigurationWriterTests()
    {
        outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.yaml");
    }

    [Fact]
    public async Task WriteAsync_ProducesFileTheLoaderCanReadBack()
    {
        var mapping = new GeneratedStyleMapping(
            new Dictionary<BlockStyleKey, string?>
            {
                [BlockStyleKey.Paragraph] = "Normal",
                [BlockStyleKey.Heading1] = "Heading1",
                [BlockStyleKey.CodeBlock] = null,
            },
            new Dictionary<InlineStyleKey, string?>
            {
                [InlineStyleKey.Bold] = null,
                [InlineStyleKey.Italic] = null,
                [InlineStyleKey.Code] = null,
            });

        await writer.WriteAsync(outputPath, mapping);

        var loader = new StyleMappingConfigurationLoader();
        var loaded = await loader.LoadAsync(outputPath);

        Assert.Equal("Normal", loaded.Blocks[BlockStyleKey.Paragraph]);
        Assert.Equal("Heading1", loaded.Blocks[BlockStyleKey.Heading1]);

        // A blank block value round-trips to "unconfigured" (dropped), not a present-but-null entry.
        Assert.False(loaded.Blocks.ContainsKey(BlockStyleKey.CodeBlock));

        // A blank inline value round-trips to present-but-null.
        Assert.True(loaded.Inlines.ContainsKey(InlineStyleKey.Code));
        Assert.Null(loaded.Inlines[InlineStyleKey.Code]);
    }

    public void Dispose()
    {
        if (File.Exists(outputPath))
            File.Delete(outputPath);
    }
}
