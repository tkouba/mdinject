using Mdinject.Core.Configuration;
using Mdinject.Core.Styles;

namespace Mdinject.Core.Tests.Configuration;

public sealed class StyleMappingConfigurationGeneratorTests
{
    private readonly StyleMappingConfigurationGenerator generator = new();

    private static readonly IReadOnlyList<StyleInfo> TemplateStyles =
    [
        new StyleInfo("Normal", "Normal", StyleKind.Paragraph, true, null, [], false),
        new StyleInfo("Nadpis1", "heading 1", StyleKind.Paragraph, false, "Normal", [], false),
        new StyleInfo("Nadpis3", "heading 3", StyleKind.Paragraph, false, "Normal", [], false),
        new StyleInfo("Normlntabulka", "Normal Table", StyleKind.Table, true, null, [], false),
    ];

    [Fact]
    public void Generate_FillsAutoDefaultBlocksWithName()
    {
        // A corporate template's own id (e.g. "Normlntabulka") is unreadable - the generated file
        // should prefer the human name whenever the style has no alias.
        var mapping = generator.Generate(TemplateStyles);

        Assert.Equal("Normal", mapping.Blocks[BlockStyleKey.Paragraph]);
        Assert.Equal("Normal Table", mapping.Blocks[BlockStyleKey.Table]);
    }

    [Fact]
    public void Generate_FillsHeadingsWhenCanonicalNameExistsWithName()
    {
        var mapping = generator.Generate(TemplateStyles);

        Assert.Equal("heading 1", mapping.Blocks[BlockStyleKey.Heading1]);
        Assert.Equal("heading 3", mapping.Blocks[BlockStyleKey.Heading3]);
    }

    [Fact]
    public void Generate_PrefersAliasOverName()
    {
        IReadOnlyList<StyleInfo> stylesWithAlias =
        [
            new StyleInfo("Normlntabulka", "Normal Table", StyleKind.Table, true, null, ["My Table"], false),
        ];

        var mapping = generator.Generate(stylesWithAlias);

        Assert.Equal("My Table", mapping.Blocks[BlockStyleKey.Table]);
    }

    [Fact]
    public void Generate_LeavesUnresolvableHeadingsBlank()
    {
        // No "heading 2" style exists in the fixture.
        var mapping = generator.Generate(TemplateStyles);

        Assert.True(mapping.Blocks.ContainsKey(BlockStyleKey.Heading2));
        Assert.Null(mapping.Blocks[BlockStyleKey.Heading2]);
    }

    [Fact]
    public void Generate_LeavesCodeBlockBlank()
    {
        var mapping = generator.Generate(TemplateStyles);

        Assert.True(mapping.Blocks.ContainsKey(BlockStyleKey.CodeBlock));
        Assert.Null(mapping.Blocks[BlockStyleKey.CodeBlock]);
    }

    [Fact]
    public void Generate_LeavesAllInlinesBlank()
    {
        var mapping = generator.Generate(TemplateStyles);

        Assert.Equal(4, mapping.Inlines.Count);
        Assert.All(mapping.Inlines.Values, Assert.Null);
    }

    [Fact]
    public void Generate_FillsLinkWhenCanonicalHyperlinkStyleExists()
    {
        IReadOnlyList<StyleInfo> stylesWithHyperlink =
        [
            .. TemplateStyles,
            new StyleInfo("Hyperlink0", "Hyperlink", StyleKind.Character, false, null, ["Link"], false),
        ];

        var mapping = generator.Generate(stylesWithHyperlink);

        Assert.Equal("Link", mapping.Inlines[InlineStyleKey.Link]);
    }
}
