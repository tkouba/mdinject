using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Mdinject.Core.Configuration;
using Mdinject.Core.DocumentModel;
using Mdinject.Core.Styles;
using MarkdownDocument = Mdinject.Core.DocumentModel.Document;

namespace Mdinject.Docx.Tests;

public sealed class DocxInjectorTests : IDisposable
{
    private readonly string templatePath;
    private readonly string outputPath;
    private readonly DocxInjector injector;

    public DocxInjectorTests()
    {
        templatePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
        outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
        CreateTestTemplate(templatePath);

        injector = new DocxInjector(new DocxTemplateDocumentFactory(), new StyleResolver());
    }

    [Fact]
    public async Task InjectAsync_ReplacesPlaceholderAndPreservesSurroundingContent()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Heading(1, [new InlineSpan("Title", false, false, false)]),
            new DocumentBlock.Paragraph([new InlineSpan("Body text.", false, false, false)]),
        ]);

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var paragraphs = result.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToList();
        var texts = paragraphs.Select(GetText).ToList();

        Assert.Equal(["Before", "Title", "Body text.", "After"], texts);
        Assert.DoesNotContain(texts, t => t.Contains("{{CONTENT}}"));
    }

    [Fact]
    public async Task InjectAsync_ResolvesHeadingAndParagraphStyles()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Heading(1, [new InlineSpan("Title", false, false, false)]),
            new DocumentBlock.Paragraph([new InlineSpan("Body text.", false, false, false)]),
        ]);

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var paragraphs = result.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToList();

        var heading = paragraphs.Single(p => GetText(p) == "Title");
        Assert.Equal("Heading1", heading.ParagraphProperties?.ParagraphStyleId?.Val);

        var body = paragraphs.Single(p => GetText(p) == "Body text.");
        Assert.Equal("Normal", body.ParagraphProperties?.ParagraphStyleId?.Val);
    }

    [Fact]
    public async Task InjectAsync_AppliesDirectBoldAndItalicFormatting()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Paragraph(
            [
                new InlineSpan("bold", true, false, false),
                new InlineSpan("italic", false, true, false),
            ]),
        ]);

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var runs = result.MainDocumentPart!.Document!.Body!.Descendants<Run>()
            .Where(r => r.InnerText is "bold" or "italic")
            .ToList();

        var boldRun = runs.Single(r => r.InnerText == "bold");
        Assert.NotNull(boldRun.RunProperties?.Bold);
        Assert.Null(boldRun.RunProperties?.Italic);

        var italicRun = runs.Single(r => r.InnerText == "italic");
        Assert.NotNull(italicRun.RunProperties?.Italic);
        Assert.Null(italicRun.RunProperties?.Bold);
    }

    [Fact]
    public async Task InjectAsync_AppliesConfiguredNamedStyleToCode()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Paragraph([new InlineSpan("snippet", false, false, true)]),
        ]);

        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string>(),
            new Dictionary<InlineStyleKey, string?> { [InlineStyleKey.Code] = "Code" });

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, configuration);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var run = result.MainDocumentPart!.Document!.Body!.Descendants<Run>().Single(r => r.InnerText == "snippet");

        Assert.Equal("CodeCharacter", run.RunProperties?.RunStyle?.Val);
    }

    [Fact]
    public async Task InjectAsync_SplitsCodeBlockLinesWithBreaks()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.CodeBlock("line one\nline two"),
        ]);

        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.CodeBlock] = "Code" },
            new Dictionary<InlineStyleKey, string?>());

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, configuration);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var paragraph = result.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().Single(p => GetText(p) == "line oneline two");

        Assert.Equal("CodeParagraph", paragraph.ParagraphProperties?.ParagraphStyleId?.Val);
        Assert.Single(paragraph.Descendants<Break>());
    }

    [Fact]
    public async Task InjectAsync_LinkWithConfiguredStyle_WrapsRunInHyperlinkWithRelationshipAndStyle()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Paragraph([new InlineSpan("Example", false, false, false, "https://example.com/")]),
        ]);

        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string>(),
            new Dictionary<InlineStyleKey, string?> { [InlineStyleKey.Link] = "Code" });

        var warnings = await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, configuration);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var mainPart = result.MainDocumentPart!;
        var hyperlink = mainPart.Document!.Body!.Descendants<Hyperlink>().Single();
        var run = hyperlink.Descendants<Run>().Single();

        Assert.Equal("Example", run.InnerText);
        Assert.Equal("CodeCharacter", run.RunProperties?.RunStyle?.Val);
        Assert.Empty(warnings);

        var relationship = mainPart.HyperlinkRelationships.Single(r => r.Id == hyperlink.Id);
        Assert.Equal("https://example.com/", relationship.Uri.ToString());
        Assert.True(relationship.IsExternal);
    }

    [Fact]
    public async Task InjectAsync_LinkWithoutConfiguration_StillWrapsRunInHyperlinkAndReturnsWarning()
    {
        // Unlike code, a link works without any style configuration - the test template defines no
        // "Hyperlink" style, so this falls back to an unstyled (but still clickable) hyperlink, and
        // that automatic fallback is reported back as a warning.
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Paragraph([new InlineSpan("Example", false, false, false, "https://example.com/")]),
        ]);

        var warnings = await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var hyperlink = result.MainDocumentPart!.Document!.Body!.Descendants<Hyperlink>().Single();
        var run = hyperlink.Descendants<Run>().Single();

        Assert.Equal("Example", run.InnerText);
        Assert.Null(run.RunProperties);
        Assert.Single(warnings);
    }

    [Fact]
    public async Task InjectAsync_LinkExplicitlyBlankConfiguration_WrapsRunInHyperlinkWithoutNamedStyleOrWarning()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Paragraph([new InlineSpan("Example", false, false, false, "https://example.com/")]),
        ]);

        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string>(),
            new Dictionary<InlineStyleKey, string?> { [InlineStyleKey.Link] = null });

        var warnings = await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, configuration);
        Assert.Empty(warnings);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var hyperlink = result.MainDocumentPart!.Document!.Body!.Descendants<Hyperlink>().Single();
        var run = hyperlink.Descendants<Run>().Single();

        Assert.Equal("Example", run.InnerText);
        Assert.Null(run.RunProperties);
    }

    [Fact]
    public async Task InjectAsync_BlockquoteWithConfiguredStyle_AppliesNamedStyleToEachParagraph()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Blockquote(
            [
                [new InlineSpan("First", false, false, false)],
                [new InlineSpan("Second", false, false, false)],
            ]),
        ]);

        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.Blockquote] = "Code" },
            new Dictionary<InlineStyleKey, string?>());

        var warnings = await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, configuration);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var paragraphs = result.MainDocumentPart!.Document!.Body!.Elements<Paragraph>()
            .Where(p => GetText(p) is "First" or "Second")
            .ToList();

        Assert.Equal(2, paragraphs.Count);
        Assert.All(paragraphs, p => Assert.Equal("CodeParagraph", p.ParagraphProperties?.ParagraphStyleId?.Val));
        Assert.All(paragraphs, p => Assert.Null(p.ParagraphProperties?.Indentation));
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task InjectAsync_BlockquoteWithoutConfiguration_IndentsDirectlyAndReturnsWarning()
    {
        // The test template defines no "Quote" style, so this falls back to direct indentation.
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Blockquote([[new InlineSpan("Quoted", false, false, false)]]),
        ]);

        var warnings = await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var paragraph = result.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().Single(p => GetText(p) == "Quoted");

        Assert.Null(paragraph.ParagraphProperties?.ParagraphStyleId);
        Assert.Equal("720", paragraph.ParagraphProperties?.Indentation?.Left);
        Assert.Single(warnings);
    }

    [Fact]
    public async Task InjectAsync_BulletList_AppliesBulletNumberingToEachItem()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.List(false,
            [
                [new InlineSpan("Item A", false, false, false)],
                [new InlineSpan("Item B", false, false, false)],
            ]),
        ]);

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var mainPart = result.MainDocumentPart!;
        var paragraphs = mainPart.Document!.Body!.Elements<Paragraph>()
            .Where(p => GetText(p) is "Item A" or "Item B")
            .ToList();

        Assert.Equal(2, paragraphs.Count);
        Assert.Equal("Normal", paragraphs[0].ParagraphProperties?.ParagraphStyleId?.Val);

        var numIds = paragraphs.Select(p => p.ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value).ToList();
        Assert.Equal(numIds[0], numIds[1]);
        Assert.All(paragraphs, p => Assert.Equal(0, p.ParagraphProperties!.NumberingProperties!.NumberingLevelReference!.Val!.Value));

        var numbering = mainPart.NumberingDefinitionsPart!.Numbering!;
        var numberingInstance = numbering.Elements<NumberingInstance>().Single(n => n.NumberID!.Value == numIds[0]);
        var abstractNum = numbering.Elements<AbstractNum>().Single(a => a.AbstractNumberId!.Value == numberingInstance.AbstractNumId!.Val!.Value);
        var level = abstractNum.Elements<Level>().Single();

        Assert.Equal(NumberFormatValues.Bullet, level.NumberingFormat!.Val!.Value);
    }

    [Fact]
    public async Task InjectAsync_OrderedList_AppliesDecimalNumberingToEachItem()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.List(true,
            [
                [new InlineSpan("First", false, false, false)],
                [new InlineSpan("Second", false, false, false)],
            ]),
        ]);

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var mainPart = result.MainDocumentPart!;
        var paragraph = mainPart.Document!.Body!.Elements<Paragraph>().Single(p => GetText(p) == "First");

        var numId = paragraph.ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value;
        var numbering = mainPart.NumberingDefinitionsPart!.Numbering!;
        var numberingInstance = numbering.Elements<NumberingInstance>().Single(n => n.NumberID!.Value == numId);
        var abstractNum = numbering.Elements<AbstractNum>().Single(a => a.AbstractNumberId!.Value == numberingInstance.AbstractNumId!.Val!.Value);
        var level = abstractNum.Elements<Level>().Single();

        Assert.Equal(NumberFormatValues.Decimal, level.NumberingFormat!.Val!.Value);
        Assert.Equal("%1.", level.LevelText!.Val!.Value);
    }

    [Fact]
    public async Task InjectAsync_TwoSeparateLists_GetIndependentNumIds()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.List(false, [[new InlineSpan("A", false, false, false)]]),
            new DocumentBlock.Paragraph([new InlineSpan("Between the two lists.", false, false, false)]),
            new DocumentBlock.List(false, [[new InlineSpan("B", false, false, false)]]),
        ]);

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var mainPart = result.MainDocumentPart!;
        var firstItem = mainPart.Document!.Body!.Elements<Paragraph>().Single(p => GetText(p) == "A");
        var secondItem = mainPart.Document!.Body!.Elements<Paragraph>().Single(p => GetText(p) == "B");

        var firstNumId = firstItem.ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value;
        var secondNumId = secondItem.ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value;

        // Separate markdown lists never share a numId - if they did, the second list would
        // continue counting from the first instead of each restarting at "1.".
        Assert.NotEqual(firstNumId, secondNumId);
    }

    [Fact]
    public async Task InjectAsync_UnconfiguredCodeBlock_ThrowsStyleResolutionException()
    {
        var document = new MarkdownDocument([new DocumentBlock.CodeBlock("x")]);

        await Assert.ThrowsAsync<StyleResolutionException>(
            () => injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty));
    }

    [Fact]
    public async Task InjectAsync_MissingPlaceholder_ThrowsPlaceholderNotFoundException()
    {
        var document = new MarkdownDocument([new DocumentBlock.Paragraph([new InlineSpan("x", false, false, false)])]);

        await Assert.ThrowsAsync<Core.PlaceholderNotFoundException>(
            () => injector.InjectAsync(templatePath, outputPath, "MISSING", document, StyleMappingConfiguration.Empty));
    }

    [Fact]
    public async Task InjectAsync_MissingPlaceholder_DoesNotLeaveOutputFileBehind()
    {
        var document = new MarkdownDocument([new DocumentBlock.Paragraph([new InlineSpan("x", false, false, false)])]);

        await Assert.ThrowsAsync<Core.PlaceholderNotFoundException>(
            () => injector.InjectAsync(templatePath, outputPath, "MISSING", document, StyleMappingConfiguration.Empty));

        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task InjectAsync_FailedInjection_DoesNotOverwritePreexistingOutputFile()
    {
        var originalBytes = new byte[] { 1, 2, 3 };
        await File.WriteAllBytesAsync(outputPath, originalBytes);

        var document = new MarkdownDocument([new DocumentBlock.Paragraph([new InlineSpan("x", false, false, false)])]);

        await Assert.ThrowsAsync<Core.PlaceholderNotFoundException>(
            () => injector.InjectAsync(templatePath, outputPath, "MISSING", document, StyleMappingConfiguration.Empty));

        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(outputPath));
    }

    [Fact]
    public async Task InjectAsync_DoesNotModifyTemplateFile()
    {
        var originalBytes = await File.ReadAllBytesAsync(templatePath);

        var document = new MarkdownDocument([new DocumentBlock.Paragraph([new InlineSpan("x", false, false, false)])]);
        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty);

        var templateBytesAfter = await File.ReadAllBytesAsync(templatePath);
        Assert.Equal(originalBytes, templateBytesAfter);
    }

    public void Dispose()
    {
        if (File.Exists(templatePath))
            File.Delete(templatePath);

        if (File.Exists(outputPath))
            File.Delete(outputPath);
    }

    private static string GetText(Paragraph paragraph)
    {
        return String.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
    }

    private static void CreateTestTemplate(string path)
    {
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);

        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new Body(
            new Paragraph(new Run(new Text("Before"))),
            new Paragraph(new Run(new Text("{{CONTENT}}"))),
            new Paragraph(new Run(new Text("After")))));

        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        var normal = new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
        normal.Append(new StyleName { Val = "Normal" });
        styles.Append(normal);

        var heading1 = new Style { Type = StyleValues.Paragraph, StyleId = "Heading1" };
        heading1.Append(new StyleName { Val = "heading 1" });
        heading1.Append(new BasedOn { Val = "Normal" });
        styles.Append(heading1);

        var codeParagraphStyle = new Style { Type = StyleValues.Paragraph, StyleId = "CodeParagraph" };
        codeParagraphStyle.Append(new StyleName { Val = "Code" });
        codeParagraphStyle.Append(new BasedOn { Val = "Normal" });
        styles.Append(codeParagraphStyle);

        var defaultFont = new Style { Type = StyleValues.Character, StyleId = "DefaultParagraphFont", Default = true };
        defaultFont.Append(new StyleName { Val = "Default Paragraph Font" });
        styles.Append(defaultFont);

        var codeCharacterStyle = new Style { Type = StyleValues.Character, StyleId = "CodeCharacter" };
        codeCharacterStyle.Append(new StyleName { Val = "Code" });
        codeCharacterStyle.Append(new BasedOn { Val = "DefaultParagraphFont" });
        styles.Append(codeCharacterStyle);

        stylesPart.Styles = styles;
        stylesPart.Styles.Save();
        mainPart.Document.Save();
    }
}
