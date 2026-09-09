using Mdinject.Core.Configuration;
using Mdinject.Core.Styles;

namespace Mdinject.Core.Tests.Styles;

public sealed class StyleResolverTests
{
    private readonly StyleResolver resolver = new();

    private static readonly IReadOnlyList<StyleInfo> TemplateStyles =
    [
        new StyleInfo("Normal", "Normal", StyleKind.Paragraph, true, null, [], false),
        new StyleInfo("Heading1", "heading 1", StyleKind.Paragraph, false, "Normal", ["H1"], false),
        new StyleInfo("MyQuote", "Quote", StyleKind.Paragraph, false, "Normal", [], true),
        new StyleInfo("Standardnpsmoodstavce", "Default Paragraph Font", StyleKind.Character, true, null, [], false),
        new StyleInfo("VYRAZNE", "Strong", StyleKind.Character, false, "Standardnpsmoodstavce", ["Bold Text"], false),
        new StyleInfo("NormalTable", "Normal Table", StyleKind.Table, true, null, [], false),
    ];

    [Fact]
    public void ResolveBlockStyle_WithoutConfiguration_ResolvesDefaultByName()
    {
        var styleId = resolver.ResolveBlockStyle(BlockStyleKey.Heading1, StyleMappingConfiguration.Empty, TemplateStyles);

        Assert.Equal("Heading1", styleId);
    }

    [Fact]
    public void ResolveBlockStyle_WithConfiguredAlias_ResolvesViaAlias()
    {
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.Heading1] = "H1" },
            new Dictionary<InlineStyleKey, string?>());

        var styleId = resolver.ResolveBlockStyle(BlockStyleKey.Heading1, configuration, TemplateStyles);

        Assert.Equal("Heading1", styleId);
    }

    [Fact]
    public void ResolveBlockStyle_WithConfiguredNormalizedId_ResolvesViaId()
    {
        // "my quote" (space, lowercase) should still match style id "MyQuote" once normalized.
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.Paragraph] = "my quote" },
            new Dictionary<InlineStyleKey, string?>());

        var styleId = resolver.ResolveBlockStyle(BlockStyleKey.Paragraph, configuration, TemplateStyles);

        Assert.Equal("MyQuote", styleId);
    }

    [Fact]
    public void ResolveBlockStyle_WithUnresolvableConfiguredReference_ThrowsStyleResolutionException()
    {
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.Paragraph] = "No Such Style" },
            new Dictionary<InlineStyleKey, string?>());

        Assert.Throws<StyleResolutionException>(
            () => resolver.ResolveBlockStyle(BlockStyleKey.Paragraph, configuration, TemplateStyles));
    }

    [Fact]
    public void ResolveBlockStyle_WithUnresolvableDefaultReference_ThrowsStyleResolutionException()
    {
        // No "heading 2" style exists in the fixture, and it's unconfigured.
        Assert.Throws<StyleResolutionException>(
            () => resolver.ResolveBlockStyle(BlockStyleKey.Heading2, StyleMappingConfiguration.Empty, TemplateStyles));
    }

    [Fact]
    public void ResolveBlockStyle_UnconfiguredParagraph_ResolvesTemplateDefault()
    {
        var styleId = resolver.ResolveBlockStyle(BlockStyleKey.Paragraph, StyleMappingConfiguration.Empty, TemplateStyles);

        Assert.Equal("Normal", styleId);
    }

    [Fact]
    public void ResolveBlockStyle_UnconfiguredTable_ResolvesTemplateDefault()
    {
        var styleId = resolver.ResolveBlockStyle(BlockStyleKey.Table, StyleMappingConfiguration.Empty, TemplateStyles);

        Assert.Equal("NormalTable", styleId);
    }

    [Fact]
    public void ResolveBlockStyle_UnconfiguredCodeBlock_ThrowsStyleResolutionException()
    {
        // CodeBlock has no native Word default and no canonical name to guess.
        Assert.Throws<StyleResolutionException>(
            () => resolver.ResolveBlockStyle(BlockStyleKey.CodeBlock, StyleMappingConfiguration.Empty, TemplateStyles));
    }

    [Fact]
    public void ResolveInlineStyle_WithConfiguredStyleName_ResolvesNamedStyle()
    {
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string>(),
            new Dictionary<InlineStyleKey, string?> { [InlineStyleKey.Code] = "Strong" });

        var resolution = resolver.ResolveInlineStyle(InlineStyleKey.Code, configuration, TemplateStyles);

        var namedStyle = Assert.IsType<InlineStyleResolution.NamedStyle>(resolution);
        Assert.Equal("VYRAZNE", namedStyle.StyleId);
    }

    [Theory]
    [InlineData(InlineStyleKey.Bold)]
    [InlineData(InlineStyleKey.Italic)]
    public void ResolveInlineStyle_UnconfiguredBoldOrItalic_ReturnsDirectFormatting(InlineStyleKey key)
    {
        var resolution = resolver.ResolveInlineStyle(key, StyleMappingConfiguration.Empty, TemplateStyles);

        Assert.IsType<InlineStyleResolution.DirectFormatting>(resolution);
    }

    [Theory]
    [InlineData(InlineStyleKey.Bold)]
    [InlineData(InlineStyleKey.Italic)]
    public void ResolveInlineStyle_ExplicitlyBlankBoldOrItalic_ReturnsDirectFormatting(InlineStyleKey key)
    {
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string>(),
            new Dictionary<InlineStyleKey, string?> { [key] = null });

        var resolution = resolver.ResolveInlineStyle(key, configuration, TemplateStyles);

        Assert.IsType<InlineStyleResolution.DirectFormatting>(resolution);
    }

    [Fact]
    public void ResolveInlineStyle_UnconfiguredCode_ThrowsStyleResolutionException()
    {
        // Code has no native default: an absent key errors as soon as it's actually resolved (used).
        Assert.Throws<StyleResolutionException>(
            () => resolver.ResolveInlineStyle(InlineStyleKey.Code, StyleMappingConfiguration.Empty, TemplateStyles));
    }

    [Fact]
    public void ResolveInlineStyle_ExplicitlyBlankCode_ReturnsNoFormatting()
    {
        // "code:" present with no value is a deliberate opt-out, distinct from the key being absent.
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string>(),
            new Dictionary<InlineStyleKey, string?> { [InlineStyleKey.Code] = null });

        var resolution = resolver.ResolveInlineStyle(InlineStyleKey.Code, configuration, TemplateStyles);

        Assert.IsType<InlineStyleResolution.NoFormatting>(resolution);
    }

    [Fact]
    public void ResolveInlineStyle_UnconfiguredLink_NoHyperlinkStyleInTemplate_ReturnsNoFormatting()
    {
        // Unlike Code, a link is functional without any named style, so an absent key never errors.
        var resolution = resolver.ResolveInlineStyle(InlineStyleKey.Link, StyleMappingConfiguration.Empty, TemplateStyles);

        Assert.IsType<InlineStyleResolution.NoFormatting>(resolution);
    }

    [Fact]
    public void ResolveInlineStyle_UnconfiguredLink_GuessesCanonicalHyperlinkStyle()
    {
        IReadOnlyList<StyleInfo> stylesWithHyperlink =
        [
            .. TemplateStyles,
            new StyleInfo("Hyperlink0", "Hyperlink", StyleKind.Character, false, null, [], false),
        ];

        var resolution = resolver.ResolveInlineStyle(InlineStyleKey.Link, StyleMappingConfiguration.Empty, stylesWithHyperlink);

        var namedStyle = Assert.IsType<InlineStyleResolution.NamedStyle>(resolution);
        Assert.Equal("Hyperlink0", namedStyle.StyleId);
    }

    [Fact]
    public void ResolveInlineStyle_ExplicitlyBlankLink_ReturnsNoFormatting()
    {
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string>(),
            new Dictionary<InlineStyleKey, string?> { [InlineStyleKey.Link] = null });

        var resolution = resolver.ResolveInlineStyle(InlineStyleKey.Link, configuration, TemplateStyles);

        Assert.IsType<InlineStyleResolution.NoFormatting>(resolution);
    }

    [Fact]
    public void ResolveInlineStyle_WithConfiguredLinkStyleName_ResolvesNamedStyle()
    {
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string>(),
            new Dictionary<InlineStyleKey, string?> { [InlineStyleKey.Link] = "Strong" });

        var resolution = resolver.ResolveInlineStyle(InlineStyleKey.Link, configuration, TemplateStyles);

        var namedStyle = Assert.IsType<InlineStyleResolution.NamedStyle>(resolution);
        Assert.Equal("VYRAZNE", namedStyle.StyleId);
    }

    [Fact]
    public void ResolveInlineStyle_WithUnresolvableConfiguredReference_ThrowsStyleResolutionException()
    {
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string>(),
            new Dictionary<InlineStyleKey, string?> { [InlineStyleKey.Bold] = "No Such Style" });

        Assert.Throws<StyleResolutionException>(
            () => resolver.ResolveInlineStyle(InlineStyleKey.Bold, configuration, TemplateStyles));
    }
}
