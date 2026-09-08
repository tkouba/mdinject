using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Mdinject.Core.DocumentModel;

/// <summary>
/// Converts Markdown text into the internal document model using Markdig. Deliberately supports
/// only the "core skeleton" constructs for now: headings, paragraphs, unordered lists, fenced/indented
/// code blocks, and inline bold/italic/code. Anything else (tables, images, ordered lists, alerts)
/// throws <see cref="MarkdownConversionException"/> rather than silently dropping content.
/// </summary>
public sealed class MarkdownDocumentParser : IMarkdownDocumentParser
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().Build();

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
            CodeBlock code => new DocumentBlock.CodeBlock(ExtractCodeText(code)),
            _ => throw new MarkdownConversionException($"Unsupported Markdown block type '{block.GetType().Name}'."),
        };
    }

    private static DocumentBlock.BulletList ConvertList(ListBlock list)
    {
        if (list.IsOrdered)
            throw new MarkdownConversionException("Ordered lists are not supported yet.");

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

        return new DocumentBlock.BulletList(items);
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

        AppendInlines(container, bold: false, italic: false, spans);
        return spans;
    }

    private static void AppendInlines(ContainerInline container, bool bold, bool italic, List<InlineSpan> spans)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    spans.Add(new InlineSpan(literal.Content.ToString(), bold, italic, false));
                    break;

                case CodeInline code:
                    spans.Add(new InlineSpan(code.Content, bold, italic, true));
                    break;

                case LineBreakInline:
                    spans.Add(new InlineSpan("\n", bold, italic, false));
                    break;

                case EmphasisInline emphasis:
                    // CommonMark nests **/__ (bold) and */_ (italic) as separate EmphasisInline
                    // levels rather than a single node, so this only ever adds one flag at a time.
                    var addsBold = emphasis.DelimiterCount == 2;
                    var addsItalic = emphasis.DelimiterCount == 1;
                    AppendInlines(emphasis, bold || addsBold, italic || addsItalic, spans);
                    break;

                case ContainerInline nested:
                    AppendInlines(nested, bold, italic, spans);
                    break;

                default:
                    throw new MarkdownConversionException($"Unsupported Markdown inline type '{inline.GetType().Name}'.");
            }
        }
    }
}
