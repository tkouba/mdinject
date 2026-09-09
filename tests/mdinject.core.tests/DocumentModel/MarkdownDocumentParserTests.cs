using Mdinject.Core.DocumentModel;

namespace Mdinject.Core.Tests.DocumentModel;

public sealed class MarkdownDocumentParserTests
{
    private readonly MarkdownDocumentParser parser = new();

    [Fact]
    public void Parse_Heading_CapturesLevelAndText()
    {
        var document = parser.Parse("## Requirements");

        var heading = Assert.IsType<DocumentBlock.Heading>(Assert.Single(document.Blocks));
        Assert.Equal(2, heading.Level);
        Assert.Equal("Requirements", Assert.Single(heading.Content).Text);
    }

    [Fact]
    public void Parse_Paragraph_CapturesPlainText()
    {
        var document = parser.Parse("Welcome.");

        var paragraph = Assert.IsType<DocumentBlock.Paragraph>(Assert.Single(document.Blocks));
        var span = Assert.Single(paragraph.Content);
        Assert.Equal("Welcome.", span.Text);
        Assert.False(span.Bold);
        Assert.False(span.Italic);
        Assert.False(span.Code);
    }

    [Fact]
    public void Parse_BoldAndItalicAndCode_SetTheRightFlags()
    {
        var document = parser.Parse("Plain **bold** *italic* `code`.");

        var paragraph = Assert.IsType<DocumentBlock.Paragraph>(Assert.Single(document.Blocks));

        Assert.Contains(paragraph.Content, s => s.Text == "bold" && s.Bold && !s.Italic && !s.Code);
        Assert.Contains(paragraph.Content, s => s.Text == "italic" && s.Italic && !s.Bold && !s.Code);
        Assert.Contains(paragraph.Content, s => s.Text == "code" && s.Code && !s.Bold && !s.Italic);
    }

    [Fact]
    public void Parse_BoldItalicCombined_SetsBothFlags()
    {
        var document = parser.Parse("***both***");

        var paragraph = Assert.IsType<DocumentBlock.Paragraph>(Assert.Single(document.Blocks));
        var span = Assert.Single(paragraph.Content);

        Assert.Equal("both", span.Text);
        Assert.True(span.Bold);
        Assert.True(span.Italic);
    }

    [Fact]
    public void Parse_BulletList_CapturesEachItemAsInlineSpans()
    {
        var document = parser.Parse(
            """
            - Item A
            - Item B
            """);

        var list = Assert.IsType<DocumentBlock.List>(Assert.Single(document.Blocks));

        Assert.False(list.Ordered);
        Assert.Equal(2, list.Items.Count);
        Assert.Equal("Item A", Assert.Single(list.Items[0]).Text);
        Assert.Equal("Item B", Assert.Single(list.Items[1]).Text);
    }

    [Fact]
    public void Parse_OrderedList_CapturesEachItemAsInlineSpans()
    {
        var document = parser.Parse(
            """
            1. First
            2. Second
            """);

        var list = Assert.IsType<DocumentBlock.List>(Assert.Single(document.Blocks));

        Assert.True(list.Ordered);
        Assert.Equal(2, list.Items.Count);
        Assert.Equal("First", Assert.Single(list.Items[0]).Text);
        Assert.Equal("Second", Assert.Single(list.Items[1]).Text);
    }

    [Fact]
    public void Parse_Blockquote_CapturesSingleParagraph()
    {
        var document = parser.Parse("> Quoted text.");

        var blockquote = Assert.IsType<DocumentBlock.Blockquote>(Assert.Single(document.Blocks));

        Assert.Equal("Quoted text.", Assert.Single(Assert.Single(blockquote.Paragraphs)).Text);
    }

    [Fact]
    public void Parse_Blockquote_CapturesMultipleParagraphs()
    {
        var document = parser.Parse(
            """
            > First paragraph.
            >
            > Second paragraph.
            """);

        var blockquote = Assert.IsType<DocumentBlock.Blockquote>(Assert.Single(document.Blocks));

        Assert.Equal(2, blockquote.Paragraphs.Count);
        Assert.Equal("First paragraph.", Assert.Single(blockquote.Paragraphs[0]).Text);
        Assert.Equal("Second paragraph.", Assert.Single(blockquote.Paragraphs[1]).Text);
    }

    [Fact]
    public void Parse_BlockquoteWithNestedList_ThrowsMarkdownConversionException()
    {
        Assert.Throws<MarkdownConversionException>(() => parser.Parse("> - Item A\n> - Item B"));
    }

    [Fact]
    public void Parse_Link_CapturesUrlOnEachSpan()
    {
        var document = parser.Parse("Plain [visit **us**](https://example.com/page) done.");

        var paragraph = Assert.IsType<DocumentBlock.Paragraph>(Assert.Single(document.Blocks));

        Assert.Contains(paragraph.Content, s => s.Text == "visit " && s.LinkUrl == "https://example.com/page" && !s.Bold);
        Assert.Contains(paragraph.Content, s => s.Text == "us" && s.LinkUrl == "https://example.com/page" && s.Bold);
        Assert.Contains(paragraph.Content, s => s.Text == "Plain " && s.LinkUrl == null);
        Assert.Contains(paragraph.Content, s => s.Text == " done." && s.LinkUrl == null);
    }

    [Fact]
    public void Parse_MailtoLink_CapturesAbsoluteUrl()
    {
        var document = parser.Parse("[contact](mailto:info@example.com)");

        var paragraph = Assert.IsType<DocumentBlock.Paragraph>(Assert.Single(document.Blocks));
        var span = Assert.Single(paragraph.Content);

        Assert.Equal("mailto:info@example.com", span.LinkUrl);
    }

    [Fact]
    public void Parse_RelativeLink_ThrowsMarkdownConversionException()
    {
        Assert.Throws<MarkdownConversionException>(() => parser.Parse("[here](./page.md)"));
    }

    [Fact]
    public void Parse_ImageLink_ThrowsMarkdownConversionException()
    {
        Assert.Throws<MarkdownConversionException>(() => parser.Parse("![alt](https://example.com/image.png)"));
    }

    [Fact]
    public void Parse_FencedCodeBlock_CapturesRawText()
    {
        var document = parser.Parse(
            """
            ```csharp
            var x = 1;
            ```
            """);

        var codeBlock = Assert.IsType<DocumentBlock.CodeBlock>(Assert.Single(document.Blocks));
        Assert.Equal("var x = 1;", codeBlock.Text.Trim());
    }

    [Fact]
    public void Parse_MultipleBlocks_PreservesOrder()
    {
        var document = parser.Parse(
            """
            # Title

            Body text.

            - One
            """);

        Assert.Equal(3, document.Blocks.Count);
        Assert.IsType<DocumentBlock.Heading>(document.Blocks[0]);
        Assert.IsType<DocumentBlock.Paragraph>(document.Blocks[1]);
        Assert.IsType<DocumentBlock.List>(document.Blocks[2]);
    }
}
