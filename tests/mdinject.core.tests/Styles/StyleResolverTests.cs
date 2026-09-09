using Mdinject.Core.Configuration;
using Mdinject.Core.DocumentModel;
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
    public void ResolveInlineStyle_UnconfiguredLink_NoHyperlinkStyleInTemplate_ReturnsNoFormattingWithWarning()
    {
        // Unlike Code, a link is functional without any named style, so an absent key never errors -
        // but since this is an automatic fallback the user didn't ask for, it carries a warning.
        var resolution = resolver.ResolveInlineStyle(InlineStyleKey.Link, StyleMappingConfiguration.Empty, TemplateStyles);

        var noFormatting = Assert.IsType<InlineStyleResolution.NoFormatting>(resolution);
        Assert.NotNull(noFormatting.Warning);
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
    public void ResolveInlineStyle_ExplicitlyBlankLink_ReturnsNoFormattingWithoutWarning()
    {
        // "link:" present with no value is a deliberate opt-out, so it shouldn't warn like the
        // fully-absent-key automatic fallback does.
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string>(),
            new Dictionary<InlineStyleKey, string?> { [InlineStyleKey.Link] = null });

        var resolution = resolver.ResolveInlineStyle(InlineStyleKey.Link, configuration, TemplateStyles);

        var noFormatting = Assert.IsType<InlineStyleResolution.NoFormatting>(resolution);
        Assert.Null(noFormatting.Warning);
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

    [Fact]
    public void ResolveBlockquoteStyle_Unconfigured_GuessesCanonicalQuoteStyle()
    {
        // TemplateStyles already defines a paragraph style named "Quote" ("MyQuote"). Unlike an
        // alert falling back to this same resolution, a plain blockquote never warns for it.
        var resolution = resolver.ResolveBlockquoteStyle(StyleMappingConfiguration.Empty, TemplateStyles);

        var namedStyle = Assert.IsType<BlockquoteStyleResolution.NamedStyle>(resolution);
        Assert.Equal("MyQuote", namedStyle.StyleId);
        Assert.Null(namedStyle.Warning);
    }

    [Fact]
    public void ResolveBlockquoteStyle_Unconfigured_NoQuoteStyleInTemplate_ReturnsDirectIndentWithWarning()
    {
        IReadOnlyList<StyleInfo> stylesWithoutQuote =
        [
            new StyleInfo("Normal", "Normal", StyleKind.Paragraph, true, null, [], false),
        ];

        var resolution = resolver.ResolveBlockquoteStyle(StyleMappingConfiguration.Empty, stylesWithoutQuote);

        var directIndent = Assert.IsType<BlockquoteStyleResolution.DirectIndent>(resolution);
        Assert.NotNull(directIndent.Warning);
    }

    [Fact]
    public void ResolveBlockquoteStyle_WithConfiguredAlias_ResolvesNamedStyle()
    {
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.Blockquote] = "H1" },
            new Dictionary<InlineStyleKey, string?>());

        var resolution = resolver.ResolveBlockquoteStyle(configuration, TemplateStyles);

        var namedStyle = Assert.IsType<BlockquoteStyleResolution.NamedStyle>(resolution);
        Assert.Equal("Heading1", namedStyle.StyleId);
    }

    [Fact]
    public void ResolveBlockquoteStyle_WithUnresolvableConfiguredReference_ThrowsStyleResolutionException()
    {
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.Blockquote] = "No Such Style" },
            new Dictionary<InlineStyleKey, string?>());

        Assert.Throws<StyleResolutionException>(
            () => resolver.ResolveBlockquoteStyle(configuration, TemplateStyles));
    }

    [Fact]
    public void ResolveAlertStyle_Unconfigured_FallsBackToBlockquoteResolutionButStillWarns()
    {
        // TemplateStyles already defines a paragraph style named "Quote" ("MyQuote") - an alert
        // with no style of its own must resolve to that same style a plain blockquote would use,
        // but (unlike a plain blockquote) it must still warn: the alert's own visual distinction
        // is lost even though a real style was found.
        var resolution = resolver.ResolveAlertStyle(AlertKind.Warning, StyleMappingConfiguration.Empty, TemplateStyles);

        var namedStyle = Assert.IsType<BlockquoteStyleResolution.NamedStyle>(resolution);
        Assert.Equal("MyQuote", namedStyle.StyleId);
        Assert.NotNull(namedStyle.Warning);
    }

    [Fact]
    public void ResolveAlertStyle_Unconfigured_NoQuoteStyleInTemplate_ReturnsDirectIndentWithWarning()
    {
        IReadOnlyList<StyleInfo> stylesWithoutQuote =
        [
            new StyleInfo("Normal", "Normal", StyleKind.Paragraph, true, null, [], false),
        ];

        var resolution = resolver.ResolveAlertStyle(AlertKind.Warning, StyleMappingConfiguration.Empty, stylesWithoutQuote);

        var directIndent = Assert.IsType<BlockquoteStyleResolution.DirectIndent>(resolution);
        Assert.NotNull(directIndent.Warning);
    }

    [Fact]
    public void ResolveAlertStyle_WithConfiguredStyleForThatKind_ResolvesNamedStyleInsteadOfFallback()
    {
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.Warning] = "H1" },
            new Dictionary<InlineStyleKey, string?>());

        var resolution = resolver.ResolveAlertStyle(AlertKind.Warning, configuration, TemplateStyles);

        var namedStyle = Assert.IsType<BlockquoteStyleResolution.NamedStyle>(resolution);
        Assert.Equal("Heading1", namedStyle.StyleId);
    }

    [Fact]
    public void ResolveAlertStyle_ConfiguredForADifferentKind_DoesNotApply()
    {
        // Configuring "note" must not affect "warning" - each alert kind is independent.
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.Note] = "H1" },
            new Dictionary<InlineStyleKey, string?>());

        var resolution = resolver.ResolveAlertStyle(AlertKind.Warning, configuration, TemplateStyles);

        var namedStyle = Assert.IsType<BlockquoteStyleResolution.NamedStyle>(resolution);
        Assert.Equal("MyQuote", namedStyle.StyleId);
    }

    [Fact]
    public void ResolveAlertStyle_WithUnresolvableConfiguredReference_ThrowsStyleResolutionException()
    {
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.Warning] = "No Such Style" },
            new Dictionary<InlineStyleKey, string?>());

        Assert.Throws<StyleResolutionException>(
            () => resolver.ResolveAlertStyle(AlertKind.Warning, configuration, TemplateStyles));
    }

    [Fact]
    public void ResolveHorizontalRuleStyle_Unconfigured_ReturnsDirectFormatting()
    {
        // Unlike Blockquote/Link, there's no canonical style name to guess, so this is the
        // intentional default rather than a degraded fallback - TemplateStyles happening to define
        // a "Quote" style must not matter here.
        var resolution = resolver.ResolveHorizontalRuleStyle(StyleMappingConfiguration.Empty, TemplateStyles);

        Assert.IsType<HorizontalRuleStyleResolution.DirectFormatting>(resolution);
    }

    [Fact]
    public void ResolveHorizontalRuleStyle_WithConfiguredAlias_ResolvesNamedStyle()
    {
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.HorizontalRule] = "H1" },
            new Dictionary<InlineStyleKey, string?>());

        var resolution = resolver.ResolveHorizontalRuleStyle(configuration, TemplateStyles);

        var namedStyle = Assert.IsType<HorizontalRuleStyleResolution.NamedStyle>(resolution);
        Assert.Equal("Heading1", namedStyle.StyleId);
    }

    [Fact]
    public void ResolveHorizontalRuleStyle_WithUnresolvableConfiguredReference_ThrowsStyleResolutionException()
    {
        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.HorizontalRule] = "No Such Style" },
            new Dictionary<InlineStyleKey, string?>());

        Assert.Throws<StyleResolutionException>(
            () => resolver.ResolveHorizontalRuleStyle(configuration, TemplateStyles));
    }
}
