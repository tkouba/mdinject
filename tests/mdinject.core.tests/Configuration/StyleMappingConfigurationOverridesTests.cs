using Mdinject.Core.Configuration;

namespace Mdinject.Core.Tests.Configuration;

public sealed class StyleMappingConfigurationOverridesTests
{
    [Fact]
    public void Apply_ToStyleMappingConfiguration_AddsNewBlockAndInlineOverrides()
    {
        var result = StyleMappingConfigurationOverrides.Apply(
            StyleMappingConfiguration.Empty,
            new Dictionary<string, string> { ["heading1"] = "Heading 1" },
            new Dictionary<string, string> { ["code"] = "Inline Code" });

        Assert.Equal("Heading 1", result.Blocks[BlockStyleKey.Heading1]);
        Assert.Equal("Inline Code", result.Inlines[InlineStyleKey.Code]);
    }

    [Fact]
    public void Apply_ToStyleMappingConfiguration_OverrideWinsOverLoadedValue()
    {
        var loaded = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.Paragraph] = "Normal" },
            new Dictionary<InlineStyleKey, string?>());

        var result = StyleMappingConfigurationOverrides.Apply(
            loaded,
            new Dictionary<string, string> { ["paragraph"] = "Quote" },
            null);

        Assert.Equal("Quote", result.Blocks[BlockStyleKey.Paragraph]);
    }

    [Fact]
    public void Apply_ToStyleMappingConfiguration_BlankInlineOverrideOptsOut()
    {
        var loaded = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string>(),
            new Dictionary<InlineStyleKey, string?> { [InlineStyleKey.Link] = "Hyperlink" });

        var result = StyleMappingConfigurationOverrides.Apply(
            loaded,
            null,
            new Dictionary<string, string> { ["link"] = "" });

        Assert.True(result.Inlines.ContainsKey(InlineStyleKey.Link));
        Assert.Null(result.Inlines[InlineStyleKey.Link]);
    }

    [Fact]
    public void Apply_ToStyleMappingConfiguration_UnknownBlockKey_ThrowsStyleMappingConfigurationException()
    {
        Assert.Throws<StyleMappingConfigurationException>(() =>
            StyleMappingConfigurationOverrides.Apply(
                StyleMappingConfiguration.Empty,
                new Dictionary<string, string> { ["nosuchkey"] = "Foo" },
                null));
    }

    [Fact]
    public void Apply_ToStyleMappingConfiguration_UnknownInlineKey_ThrowsStyleMappingConfigurationException()
    {
        Assert.Throws<StyleMappingConfigurationException>(() =>
            StyleMappingConfigurationOverrides.Apply(
                StyleMappingConfiguration.Empty,
                null,
                new Dictionary<string, string> { ["nosuchkey"] = "Foo" }));
    }

    [Fact]
    public void Apply_ToGeneratedStyleMapping_OverridesBlankGuessWithValue()
    {
        var mapping = new GeneratedStyleMapping(
            new Dictionary<BlockStyleKey, string?> { [BlockStyleKey.CodeBlock] = null },
            new Dictionary<InlineStyleKey, string?> { [InlineStyleKey.Code] = null });

        var result = StyleMappingConfigurationOverrides.Apply(
            mapping,
            new Dictionary<string, string> { ["codeBlock"] = "Code" },
            new Dictionary<string, string> { ["code"] = "Inline Code" });

        Assert.Equal("Code", result.Blocks[BlockStyleKey.CodeBlock]);
        Assert.Equal("Inline Code", result.Inlines[InlineStyleKey.Code]);
    }

    [Fact]
    public void Apply_ToGeneratedStyleMapping_BlankOverrideClearsAGuessedValue()
    {
        var mapping = new GeneratedStyleMapping(
            new Dictionary<BlockStyleKey, string?> { [BlockStyleKey.Heading1] = "heading 1" },
            new Dictionary<InlineStyleKey, string?>());

        var result = StyleMappingConfigurationOverrides.Apply(
            mapping,
            new Dictionary<string, string> { ["heading1"] = "" },
            null);

        Assert.True(result.Blocks.ContainsKey(BlockStyleKey.Heading1));
        Assert.Null(result.Blocks[BlockStyleKey.Heading1]);
    }

    [Fact]
    public void Apply_ToGeneratedStyleMapping_UnknownKey_ThrowsStyleMappingConfigurationException()
    {
        Assert.Throws<StyleMappingConfigurationException>(() =>
            StyleMappingConfigurationOverrides.Apply(
                new GeneratedStyleMapping(new Dictionary<BlockStyleKey, string?>(), new Dictionary<InlineStyleKey, string?>()),
                new Dictionary<string, string> { ["nosuchkey"] = "Foo" },
                null));
    }
}
