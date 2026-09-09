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
    private readonly string imagesDirectory;
    private readonly DocxInjector injector;

    public DocxInjectorTests()
    {
        templatePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
        outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
        CreateTestTemplate(templatePath);

        imagesDirectory = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}");
        Directory.CreateDirectory(imagesDirectory);

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
    public async Task InjectAsync_UnconfiguredAlert_FallsBackToPlainBlockquoteIndentAndWarning()
    {
        // No style configured for "warning" (or "Quote" in the template): an alert with no style
        // of its own must render like an unconfigured plain blockquote, except the "[!WARNING]"
        // marker itself stays visible in the text, since every other distinction is lost.
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Blockquote([[new InlineSpan("Danger", false, false, false)]], AlertKind.Warning),
        ]);

        var warnings = await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var paragraph = result.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().Single(p => GetText(p) == "[!WARNING]Danger");

        Assert.Null(paragraph.ParagraphProperties?.ParagraphStyleId);
        Assert.Equal("720", paragraph.ParagraphProperties?.Indentation?.Left);
        Assert.Single(warnings);

        var runs = paragraph.Elements<Run>().ToList();
        Assert.Equal("[!WARNING]", runs[0].InnerText);
        Assert.NotNull(runs[0].RunProperties?.Bold);
        Assert.NotNull(runs[1].Elements<Break>().SingleOrDefault());
    }

    [Fact]
    public async Task InjectAsync_FallbackAlertWithMultipleParagraphs_MarkerOnlyOnFirstParagraph()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Blockquote(
            [
                [new InlineSpan("First.", false, false, false)],
                [new InlineSpan("Second.", false, false, false)],
            ],
            AlertKind.Note),
        ]);

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var paragraphs = result.MainDocumentPart!.Document!.Body!.Elements<Paragraph>()
            .Where(p => GetText(p).Contains("First.") || GetText(p) == "Second.")
            .ToList();

        Assert.Equal("[!NOTE]First.", GetText(paragraphs[0]));
        Assert.Equal("Second.", GetText(paragraphs[1]));
    }

    [Fact]
    public async Task InjectAsync_AlertWithConfiguredStyleForItsKind_AppliesNamedStyleWithoutIndentOrWarning()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Blockquote([[new InlineSpan("Danger", false, false, false)]], AlertKind.Warning),
        ]);

        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.Warning] = "Code" },
            new Dictionary<InlineStyleKey, string?>());

        var warnings = await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, configuration);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var paragraph = result.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().Single(p => GetText(p) == "Danger");

        Assert.Equal("CodeParagraph", paragraph.ParagraphProperties?.ParagraphStyleId?.Val);
        Assert.Null(paragraph.ParagraphProperties?.Indentation);
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task InjectAsync_AlertConfiguredForADifferentKind_StillFallsBack()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Blockquote([[new InlineSpan("Danger", false, false, false)]], AlertKind.Warning),
        ]);

        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.Note] = "Code" },
            new Dictionary<InlineStyleKey, string?>());

        var warnings = await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, configuration);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var paragraph = result.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().Single(p => GetText(p) == "[!WARNING]Danger");

        Assert.Null(paragraph.ParagraphProperties?.ParagraphStyleId);
        Assert.Equal("720", paragraph.ParagraphProperties?.Indentation?.Left);
        Assert.Single(warnings);
    }

    [Fact]
    public async Task InjectAsync_UnconfiguredAlertWithQuoteStyleInTemplate_UsesQuoteStyleButStillWarns()
    {
        // Unlike the shared test template (which has no "Quote" style, exercising the direct-indent
        // fallback above), this one does - proving an alert still warns even when the blockquote
        // fallback silently resolves to a real named style, since the alert's own visual
        // distinction is lost either way.
        var templateWithQuoteStylePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
        try
        {
            CreateTestTemplateWithQuoteStyle(templateWithQuoteStylePath);

            var document = new MarkdownDocument(
            [
                new DocumentBlock.Blockquote([[new InlineSpan("Danger", false, false, false)]], AlertKind.Warning),
            ]);

            var warnings = await injector.InjectAsync(templateWithQuoteStylePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty);

            using var result = WordprocessingDocument.Open(outputPath, false);
            var paragraph = result.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().Single(p => GetText(p) == "[!WARNING]Danger");

            Assert.Equal("QuoteStyle", paragraph.ParagraphProperties?.ParagraphStyleId?.Val);
            Assert.Null(paragraph.ParagraphProperties?.Indentation);
            Assert.Single(warnings);
        }
        finally
        {
            File.Delete(templateWithQuoteStylePath);
        }
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
    public async Task InjectAsync_Table_RendersHeaderAndBodyRowsWithResolvedStyles()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Table(
                HeaderCells: [[new InlineSpan("Name", false, false, false)], [new InlineSpan("Role", false, false, false)]],
                Rows:
                [
                    [[new InlineSpan("Alice", false, false, false)], [new InlineSpan("Engineer", false, false, false)]],
                    [[new InlineSpan("Bob", true, false, false)], [new InlineSpan("Manager", false, false, false)]],
                ]),
        ]);

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var table = result.MainDocumentPart!.Document!.Body!.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>().Single();

        Assert.Equal("NormalTable", table.TableProperties?.TableStyle?.Val);
        Assert.Equal(2, table.Elements<TableGrid>().Single().Elements<GridColumn>().Count());

        var rows = table.Elements<TableRow>().ToList();
        Assert.Equal(3, rows.Count);

        var headerRow = rows[0];
        Assert.NotNull(headerRow.TableRowProperties?.GetFirstChild<TableHeader>());
        var headerCells = headerRow.Elements<TableCell>().ToList();
        Assert.Equal(["Name", "Role"], headerCells.Select(GetCellText));

        var bodyRow = rows[1];
        Assert.Null(bodyRow.TableRowProperties);
        Assert.Equal(["Alice", "Engineer"], bodyRow.Elements<TableCell>().Select(GetCellText));

        var boldRun = rows[2].Elements<TableCell>().First().Descendants<Run>().Single();
        Assert.Equal("Bob", boldRun.InnerText);
        Assert.NotNull(boldRun.RunProperties?.Bold);

        var cellParagraph = headerCells[0].Elements<Paragraph>().Single();
        Assert.Equal("Normal", cellParagraph.ParagraphProperties?.ParagraphStyleId?.Val);
    }

    [Fact]
    public async Task InjectAsync_Table_IsFollowedByAnEmptyParagraph()
    {
        // A table can't be the last body content, or immediately followed by another table,
        // without an intervening paragraph.
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Table(
                HeaderCells: [[new InlineSpan("A", false, false, false)]],
                Rows: []),
        ]);

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var body = result.MainDocumentPart!.Document!.Body!;
        var table = body.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>().Single();

        Assert.IsType<Paragraph>(table.NextSibling());
    }

    [Fact]
    public async Task InjectAsync_TableWithConfiguredStyle_AppliesConfiguredTableStyle()
    {
        var document = new MarkdownDocument(
        [
            new DocumentBlock.Table(
                HeaderCells: [[new InlineSpan("A", false, false, false)]],
                Rows: []),
        ]);

        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.Table] = "Normal Table" },
            new Dictionary<InlineStyleKey, string?>());

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, configuration);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var table = result.MainDocumentPart!.Document!.Body!.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>().Single();

        Assert.Equal("NormalTable", table.TableProperties?.TableStyle?.Val);
    }

    [Fact]
    public async Task InjectAsync_HorizontalRuleWithoutConfiguration_AppliesDirectBottomBorder()
    {
        var document = new MarkdownDocument([new DocumentBlock.HorizontalRule()]);

        var warnings = await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var paragraphs = result.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToList();
        var paragraph = paragraphs.Single(p => p.ParagraphProperties?.ParagraphBorders != null);

        var bottomBorder = paragraph.ParagraphProperties!.ParagraphBorders!.BottomBorder!;
        Assert.Equal(BorderValues.Single, bottomBorder.Val!.Value);
        Assert.Null(paragraph.ParagraphProperties.ParagraphStyleId);
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task InjectAsync_HorizontalRuleWithConfiguredStyle_AppliesNamedStyleWithoutBorder()
    {
        var document = new MarkdownDocument([new DocumentBlock.HorizontalRule()]);

        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.HorizontalRule] = "Code" },
            new Dictionary<InlineStyleKey, string?>());

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, configuration);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var paragraph = result.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().Single(p => p.ParagraphProperties?.ParagraphStyleId?.Val == "CodeParagraph");

        Assert.Null(paragraph.ParagraphProperties!.ParagraphBorders);
    }

    [Fact]
    public async Task InjectAsync_HorizontalRuleWithUnresolvableConfiguredReference_ThrowsStyleResolutionException()
    {
        var document = new MarkdownDocument([new DocumentBlock.HorizontalRule()]);

        var configuration = new StyleMappingConfiguration(
            new Dictionary<BlockStyleKey, string> { [BlockStyleKey.HorizontalRule] = "No Such Style" },
            new Dictionary<InlineStyleKey, string?>());

        await Assert.ThrowsAsync<StyleResolutionException>(
            () => injector.InjectAsync(templatePath, outputPath, "CONTENT", document, configuration));
    }

    [Fact]
    public async Task InjectAsync_PngImage_EmbedsAtNaturalPixelSizeAndAltText()
    {
        var imagePath = Path.Combine(imagesDirectory, "diagram.png");
        CreateMinimalPng(imagePath, width: 64, height: 32);

        var document = new MarkdownDocument([new DocumentBlock.Image("diagram.png", "A diagram")]);

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty, basePath: imagesDirectory);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var mainPart = result.MainDocumentPart!;

        Assert.Single(mainPart.ImageParts);

        var docPr = mainPart.Document!.Body!.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>().Single();
        Assert.Equal("A diagram", docPr.Description);

        var extent = mainPart.Document.Body.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>().Single();
        Assert.Equal(64 * 9525L, extent.Cx!.Value);
        Assert.Equal(32 * 9525L, extent.Cy!.Value);
    }

    [Fact]
    public async Task InjectAsync_JpegImage_EmbedsAtNaturalPixelSize()
    {
        var imagePath = Path.Combine(imagesDirectory, "photo.jpg");
        CreateMinimalJpeg(imagePath, width: 100, height: 50);

        var document = new MarkdownDocument([new DocumentBlock.Image("photo.jpg", "")]);

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty, basePath: imagesDirectory);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var extent = result.MainDocumentPart!.Document!.Body!.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>().Single();

        Assert.Equal(100 * 9525L, extent.Cx!.Value);
        Assert.Equal(50 * 9525L, extent.Cy!.Value);
    }

    [Fact]
    public async Task InjectAsync_ImageUsesResolvedParagraphStyle()
    {
        CreateMinimalPng(Path.Combine(imagesDirectory, "diagram.png"), 10, 10);

        var document = new MarkdownDocument([new DocumentBlock.Image("diagram.png", "alt")]);

        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty, basePath: imagesDirectory);

        using var result = WordprocessingDocument.Open(outputPath, false);
        var paragraph = result.MainDocumentPart!.Document!.Body!.Descendants<Drawing>().Single().Parent!.Parent as Paragraph;

        Assert.Equal("Normal", paragraph?.ParagraphProperties?.ParagraphStyleId?.Val);
    }

    [Fact]
    public async Task InjectAsync_ImageFileMissing_ThrowsImageProcessingException()
    {
        var document = new MarkdownDocument([new DocumentBlock.Image("does-not-exist.png", "alt")]);

        await Assert.ThrowsAsync<Core.ImageProcessingException>(
            () => injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty, basePath: imagesDirectory));
    }

    [Fact]
    public async Task InjectAsync_UnsupportedImageFormat_ThrowsImageProcessingException()
    {
        var imagePath = Path.Combine(imagesDirectory, "icon.bmp");
        File.WriteAllBytes(imagePath, [0x42, 0x4D]);

        var document = new MarkdownDocument([new DocumentBlock.Image("icon.bmp", "alt")]);

        await Assert.ThrowsAsync<Core.ImageProcessingException>(
            () => injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty, basePath: imagesDirectory));
    }

    [Fact]
    public async Task InjectAsync_AbsoluteImagePath_IgnoresBasePath()
    {
        var absolutePath = Path.Combine(imagesDirectory, "absolute.png");
        CreateMinimalPng(absolutePath, 20, 20);

        var document = new MarkdownDocument([new DocumentBlock.Image(absolutePath, "alt")]);

        // basePath points elsewhere entirely - an absolute image Source must not be combined with it.
        await injector.InjectAsync(templatePath, outputPath, "CONTENT", document, StyleMappingConfiguration.Empty, basePath: Path.GetTempPath());

        using var result = WordprocessingDocument.Open(outputPath, false);
        Assert.Single(result.MainDocumentPart!.ImageParts);
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

        if (Directory.Exists(imagesDirectory))
            Directory.Delete(imagesDirectory, recursive: true);
    }

    private static string GetText(Paragraph paragraph)
    {
        return String.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
    }

    /// <summary>
    /// Writes just enough of a PNG file for <c>ImageEmbedder</c>'s dimension reader (which only
    /// ever looks at the signature and the IHDR chunk's width/height) - not a real, decodable image.
    /// </summary>
    private static void CreateMinimalPng(string path, int width, int height)
    {
        var bytes = new List<byte>();
        bytes.AddRange([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]); // PNG signature
        bytes.AddRange(BigEndianBytes(13)); // IHDR chunk data length
        bytes.AddRange("IHDR"u8.ToArray());
        bytes.AddRange(BigEndianBytes(width));
        bytes.AddRange(BigEndianBytes(height));
        bytes.AddRange([8, 2, 0, 0, 0]); // bit depth, color type, compression, filter, interlace
        bytes.AddRange([0, 0, 0, 0]); // CRC (not validated by our reader)

        File.WriteAllBytes(path, bytes.ToArray());
    }

    /// <summary>
    /// Writes just enough of a JPEG file for <c>ImageEmbedder</c>'s dimension reader (which stops
    /// as soon as it finds a SOF0 marker) - not a real, decodable image.
    /// </summary>
    private static void CreateMinimalJpeg(string path, int width, int height)
    {
        var bytes = new List<byte>();
        bytes.AddRange([0xFF, 0xD8]); // SOI
        bytes.AddRange([0xFF, 0xC0]); // SOF0
        bytes.AddRange([0x00, 0x0B]); // segment length (not used by our reader)
        bytes.Add(0x08); // precision
        bytes.AddRange(BigEndianUInt16Bytes(height));
        bytes.AddRange(BigEndianUInt16Bytes(width));

        File.WriteAllBytes(path, bytes.ToArray());
    }

    private static IEnumerable<byte> BigEndianBytes(int value)
    {
        return [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
    }

    private static IEnumerable<byte> BigEndianUInt16Bytes(int value)
    {
        return [(byte)(value >> 8), (byte)value];
    }

    private static string GetCellText(TableCell cell)
    {
        return String.Concat(cell.Descendants<Text>().Select(t => t.Text));
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

        var defaultTableStyle = new Style { Type = StyleValues.Table, StyleId = "NormalTable", Default = true };
        defaultTableStyle.Append(new StyleName { Val = "Normal Table" });
        styles.Append(defaultTableStyle);

        stylesPart.Styles = styles;
        stylesPart.Styles.Save();
        mainPart.Document.Save();
    }

    private static void CreateTestTemplateWithQuoteStyle(string path)
    {
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);

        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new Body(
            new Paragraph(new Run(new Text("{{CONTENT}}")))));

        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        var normal = new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
        normal.Append(new StyleName { Val = "Normal" });
        styles.Append(normal);

        var quote = new Style { Type = StyleValues.Paragraph, StyleId = "QuoteStyle" };
        quote.Append(new StyleName { Val = "Quote" });
        quote.Append(new BasedOn { Val = "Normal" });
        styles.Append(quote);

        stylesPart.Styles = styles;
        stylesPart.Styles.Save();
        mainPart.Document.Save();
    }
}
