using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Mdinject.Core;
using Mdinject.Core.Configuration;
using Mdinject.Core.DocumentModel;
using Mdinject.Core.Styles;
using MarkdownDocument = Mdinject.Core.DocumentModel.Document;

namespace Mdinject.Docx;

/// <summary>
/// Injects a parsed <see cref="MarkdownDocument"/> into a copy of a Word template at a named
/// placeholder paragraph. Supports headings, paragraphs, code blocks, and inline bold/italic/code
/// so far - bullet lists (and anything else in the document model) throw
/// <see cref="NotSupportedException"/> until a later pass adds them.
/// </summary>
public sealed class DocxInjector : IDocumentInjector
{
    private readonly ITemplateDocumentFactory templateDocumentFactory;
    private readonly IStyleResolver styleResolver;

    public DocxInjector(ITemplateDocumentFactory templateDocumentFactory, IStyleResolver styleResolver)
    {
        this.templateDocumentFactory = templateDocumentFactory;
        this.styleResolver = styleResolver;
    }

    public async Task InjectAsync(
        string templatePath,
        string outputPath,
        string placeholder,
        MarkdownDocument document,
        StyleMappingConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StyleInfo> templateStyles;
        using (var templateDocument = templateDocumentFactory.Open(templatePath))
        {
            templateStyles = await templateDocument.GetStylesAsync(cancellationToken);
        }

        File.Copy(templatePath, outputPath, overwrite: true);

        using var wordDocument = WordprocessingDocument.Open(outputPath, true);
        var mainPart = wordDocument.MainDocumentPart ?? throw new InvalidOperationException("Template has no main document part.");
        var body = mainPart.Document?.Body ?? throw new InvalidOperationException("Template document has no body.");

        var placeholderParagraph = FindPlaceholderParagraph(body, placeholder);
        if (placeholderParagraph == null)
            throw new PlaceholderNotFoundException($"Placeholder '{{{{{placeholder}}}}}' was not found in the template.");

        OpenXmlElement anchor = placeholderParagraph;
        foreach (var block in document.Blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var element in ConvertBlock(block, configuration, templateStyles))
            {
                anchor.InsertAfterSelf(element);
                anchor = element;
            }
        }

        placeholderParagraph.Remove();

        mainPart.Document.Save();
    }

    private static Paragraph? FindPlaceholderParagraph(Body body, string placeholder)
    {
        var target = "{{" + placeholder + "}}";

        return body.Elements<Paragraph>().FirstOrDefault(p => GetText(p).Trim() == target);
    }

    private static string GetText(Paragraph paragraph)
    {
        return String.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
    }

    private IEnumerable<OpenXmlElement> ConvertBlock(DocumentBlock block, StyleMappingConfiguration configuration, IReadOnlyList<StyleInfo> templateStyles)
    {
        switch (block)
        {
            case DocumentBlock.Heading heading:
                yield return BuildParagraph(HeadingKey(heading.Level), heading.Content, configuration, templateStyles);
                yield break;

            case DocumentBlock.Paragraph paragraph:
                yield return BuildParagraph(BlockStyleKey.Paragraph, paragraph.Content, configuration, templateStyles);
                yield break;

            case DocumentBlock.CodeBlock codeBlock:
                yield return BuildCodeParagraph(codeBlock, configuration, templateStyles);
                yield break;

            default:
                throw new NotSupportedException($"Document block '{block.GetType().Name}' is not supported by the injector yet.");
        }
    }

    private static BlockStyleKey HeadingKey(int level)
    {
        return level switch
        {
            1 => BlockStyleKey.Heading1,
            2 => BlockStyleKey.Heading2,
            3 => BlockStyleKey.Heading3,
            4 => BlockStyleKey.Heading4,
            5 => BlockStyleKey.Heading5,
            6 => BlockStyleKey.Heading6,
            7 => BlockStyleKey.Heading7,
            8 => BlockStyleKey.Heading8,
            9 => BlockStyleKey.Heading9,
            _ => throw new NotSupportedException($"Heading level {level} is not supported (must be 1-9)."),
        };
    }

    private Paragraph BuildParagraph(BlockStyleKey key, IReadOnlyList<InlineSpan> content, StyleMappingConfiguration configuration, IReadOnlyList<StyleInfo> templateStyles)
    {
        var styleId = styleResolver.ResolveBlockStyle(key, configuration, templateStyles);

        var paragraph = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = styleId }));

        foreach (var span in content)
            paragraph.AppendChild(BuildRun(span, configuration, templateStyles));

        return paragraph;
    }

    private Paragraph BuildCodeParagraph(DocumentBlock.CodeBlock codeBlock, StyleMappingConfiguration configuration, IReadOnlyList<StyleInfo> templateStyles)
    {
        var styleId = styleResolver.ResolveBlockStyle(BlockStyleKey.CodeBlock, configuration, templateStyles);

        var paragraph = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = styleId }));

        var lines = codeBlock.Text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            paragraph.AppendChild(new Run(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve }));

            if (i < lines.Length - 1)
                paragraph.AppendChild(new Run(new Break()));
        }

        return paragraph;
    }

    private Run BuildRun(InlineSpan span, StyleMappingConfiguration configuration, IReadOnlyList<StyleInfo> templateStyles)
    {
        var run = new Run();

        var runProperties = BuildRunProperties(span, configuration, templateStyles);
        if (runProperties != null)
            run.AppendChild(runProperties);

        run.AppendChild(new Text(span.Text) { Space = SpaceProcessingModeValues.Preserve });

        return run;
    }

    private RunProperties? BuildRunProperties(InlineSpan span, StyleMappingConfiguration configuration, IReadOnlyList<StyleInfo> templateStyles)
    {
        string? rStyleId = null;
        var bold = false;
        var italic = false;

        // A run can carry both a named rStyle and direct b/i overrides at once, but only one
        // rStyle - if Code/Bold/Italic each resolve to a different named style, the first one
        // (Code, then Bold, then Italic) wins; the others still apply as direct formatting if requested.
        if (span.Code)
            ApplyInline(InlineStyleKey.Code, configuration, templateStyles, ref rStyleId, ref bold, ref italic);

        if (span.Bold)
            ApplyInline(InlineStyleKey.Bold, configuration, templateStyles, ref rStyleId, ref bold, ref italic);

        if (span.Italic)
            ApplyInline(InlineStyleKey.Italic, configuration, templateStyles, ref rStyleId, ref bold, ref italic);

        if (rStyleId == null && !bold && !italic)
            return null;

        var runProperties = new RunProperties();

        if (rStyleId != null)
            runProperties.AppendChild(new RunStyle { Val = rStyleId });

        if (bold)
            runProperties.AppendChild(new Bold());

        if (italic)
            runProperties.AppendChild(new Italic());

        return runProperties;
    }

    private void ApplyInline(
        InlineStyleKey key,
        StyleMappingConfiguration configuration,
        IReadOnlyList<StyleInfo> templateStyles,
        ref string? rStyleId,
        ref bool bold,
        ref bool italic)
    {
        var resolution = styleResolver.ResolveInlineStyle(key, configuration, templateStyles);

        if (resolution is InlineStyleResolution.NamedStyle namedStyle)
        {
            rStyleId ??= namedStyle.StyleId;
            return;
        }

        if (resolution is InlineStyleResolution.DirectFormatting)
        {
            if (key == InlineStyleKey.Bold)
                bold = true;
            else if (key == InlineStyleKey.Italic)
                italic = true;
        }
    }
}
