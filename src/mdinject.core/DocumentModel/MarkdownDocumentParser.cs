using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Mdinject.Core.DocumentModel;

/// <summary>
/// Converts Markdown text into the internal document model using Markdig. Deliberately supports
/// only the "core skeleton" constructs for now: headings, paragraphs, bullet and numbered lists,
/// blockquotes, GitHub-style pipe tables, fenced/indented code blocks, and inline
/// bold/italic/code/links. Anything else (images, alerts) throws
/// <see cref="MarkdownConversionException"/> rather than silently dropping content. Links must
/// resolve to an absolute URL (http(s), mailto, ...) - relative links have no meaningful target
/// once the content is injected into a Word document, so they're rejected rather than silently
/// kept as broken links.
/// </summary>
public sealed class MarkdownDocumentParser : IMarkdownDocumentParser
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();

    public Document Parse(string markdown)
    {
        var markdigDocument = Markdig.Markdown.Parse(markdown, Pipeline);

        var blocks = new List<DocumentBlock>();
        foreach (var block in markdigDocument)
            blocks.Add(ConvertBlock(block));

        return new Document(blocks);
    }

    private static DocumentBlock ConvertBlock(Block block)
    {
        return block switch
        {
            HeadingBlock heading => new DocumentBlock.Heading(heading.Level, ConvertInlines(heading.Inline)),
            ParagraphBlock paragraph => new DocumentBlock.Paragraph(ConvertInlines(paragraph.Inline)),
            ListBlock list => ConvertList(list),
            QuoteBlock quote => ConvertBlockquote(quote),
            Table table => ConvertTable(table),
            CodeBlock code => new DocumentBlock.CodeBlock(ExtractCodeText(code)),
            _ => throw new MarkdownConversionException($"Unsupported Markdown block type '{block.GetType().Name}'."),
        };
    }

    private static DocumentBlock.Table ConvertTable(Table table)
    {
        IReadOnlyList<InlineSpan>[]? headerCells = null;
        var rows = new List<IReadOnlyList<IReadOnlyList<InlineSpan>>>();

        foreach (var rowBlock in table)
        {
            if (rowBlock is not TableRow row)
                throw new MarkdownConversionException($"Unsupported table row type '{rowBlock.GetType().Name}'.");

            var cells = ConvertTableRow(row);

            if (row.IsHeader)
                headerCells = cells;
            else
                rows.Add(cells);
        }

        if (headerCells == null)
            throw new MarkdownConversionException("Tables without a header row are not supported.");

        return new DocumentBlock.Table(headerCells, rows);
    }

    private static IReadOnlyList<InlineSpan>[] ConvertTableRow(TableRow row)
    {
        var cells = new IReadOnlyList<InlineSpan>[row.Count];

        for (var i = 0; i < row.Count; i++)
        {
            if (row[i] is not TableCell cell)
                throw new MarkdownConversionException($"Unsupported table cell type '{row[i].GetType().Name}'.");

            var paragraph = cell.OfType<ParagraphBlock>().FirstOrDefault();

            // Markdig's pipe-table cell parser appends a spurious trailing empty LiteralInline
            // after the last inline in some cells (e.g. one ending in bold/italic) - drop it
            // rather than emit a pointless empty run; paragraphs, lists, and blockquotes don't
            // have this quirk.
            cells[i] = paragraph != null
                ? ConvertInlines(paragraph.Inline).Where(span => span.Text.Length > 0).ToList()
                : [];
        }

        return cells;
    }

    private static DocumentBlock.Blockquote ConvertBlockquote(QuoteBlock quote)
    {
        var paragraphs = new List<IReadOnlyList<InlineSpan>>();

        foreach (var childBlock in quote)
        {
            if (childBlock is not ParagraphBlock paragraph)
                throw new MarkdownConversionException($"Unsupported blockquote content type '{childBlock.GetType().Name}'.");

            paragraphs.Add(ConvertInlines(paragraph.Inline));
        }

        return new DocumentBlock.Blockquote(paragraphs);
    }

    private static DocumentBlock.List ConvertList(ListBlock list)
    {
        var items = new List<IReadOnlyList<InlineSpan>>();

        foreach (var itemBlock in list)
        {
            if (itemBlock is not ListItemBlock listItem)
                throw new MarkdownConversionException($"Unsupported list item type '{itemBlock.GetType().Name}'.");

            var paragraph = listItem.OfType<ParagraphBlock>().FirstOrDefault();
            if (paragraph == null)
                throw new MarkdownConversionException("Only simple, single-paragraph list items are supported yet.");

            items.Add(ConvertInlines(paragraph.Inline));
        }

        return new DocumentBlock.List(list.IsOrdered, items);
    }

    private static string ExtractCodeText(CodeBlock code)
    {
        return code.Lines.ToString();
    }

    private static IReadOnlyList<InlineSpan> ConvertInlines(ContainerInline? container)
    {
        var spans = new List<InlineSpan>();
        if (container == null)
            return spans;

        AppendInlines(container, bold: false, italic: false, linkUrl: null, spans);
        return spans;
    }

    private static void AppendInlines(ContainerInline container, bool bold, bool italic, string? linkUrl, List<InlineSpan> spans)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    spans.Add(new InlineSpan(literal.Content.ToString(), bold, italic, false, linkUrl));
                    break;

                case CodeInline code:
                    spans.Add(new InlineSpan(code.Content, bold, italic, true, linkUrl));
                    break;

                case LineBreakInline:
                    spans.Add(new InlineSpan("\n", bold, italic, false, linkUrl));
                    break;

                case EmphasisInline emphasis:
                    // CommonMark nests **/__ (bold) and */_ (italic) as separate EmphasisInline
                    // levels rather than a single node, so this only ever adds one flag at a time.
                    var addsBold = emphasis.DelimiterCount == 2;
                    var addsItalic = emphasis.DelimiterCount == 1;
                    AppendInlines(emphasis, bold || addsBold, italic || addsItalic, linkUrl, spans);
                    break;

                case LinkInline { IsImage: true }:
                    throw new MarkdownConversionException("Images are not supported yet.");

                case LinkInline link:
                    if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var uri))
                        throw new MarkdownConversionException($"Link URL '{link.Url}' is not a supported absolute URL.");

                    AppendInlines(link, bold, italic, uri.AbsoluteUri, spans);
                    break;

                case ContainerInline nested:
                    AppendInlines(nested, bold, italic, linkUrl, spans);
                    break;

                default:
                    throw new MarkdownConversionException($"Unsupported Markdown inline type '{inline.GetType().Name}'.");
            }
        }
    }
}
