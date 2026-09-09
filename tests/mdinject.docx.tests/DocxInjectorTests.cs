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
