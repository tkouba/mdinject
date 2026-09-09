using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Mdinject.Core.Styles;
using Mdinject.Docx;

namespace Mdinject.Docx.Tests;

public sealed class DocxTemplateDocumentTests : IDisposable
{
    private readonly string templatePath;

    public DocxTemplateDocumentTests()
    {
        templatePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
        CreateTestTemplate(templatePath);
    }

    [Fact]
    public async Task GetStylesAsync_ReturnsStylesDefinedInTemplate()
    {
        using var templateDocument = new DocxTemplateDocument(templatePath);

        var styles = await templateDocument.GetStylesAsync();

        var normal = styles.SingleOrDefault(s => s.Id == "Normal");
        Assert.NotNull(normal);
        Assert.Equal("Normal", normal.Name);
        Assert.Equal(StyleKind.Paragraph, normal.Kind);
        Assert.True(normal.IsDefault);
        Assert.Null(normal.BasedOnId);
        Assert.Empty(normal.Aliases);
        Assert.False(normal.IsCustom);

        var heading1 = styles.SingleOrDefault(s => s.Id == "Heading1");
        Assert.NotNull(heading1);
        Assert.Equal("heading 1", heading1.Name);
        Assert.Equal(StyleKind.Paragraph, heading1.Kind);
        Assert.False(heading1.IsDefault);
        Assert.Equal("Normal", heading1.BasedOnId);
        Assert.Equal(["H1", "Section Heading"], heading1.Aliases);
        Assert.False(heading1.IsCustom);

        var defaultFont = styles.SingleOrDefault(s => s.Id == "DefaultParagraphFont");
        Assert.NotNull(defaultFont);
        Assert.Equal(StyleKind.Character, defaultFont.Kind);
        Assert.True(defaultFont.IsDefault);

        var customStyle = styles.SingleOrDefault(s => s.Id == "MyCustomStyle");
        Assert.NotNull(customStyle);
        Assert.True(customStyle.IsCustom);
    }

    [Fact]
    public async Task GetStylesAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        using var templateDocument = new DocxTemplateDocument(templatePath);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => templateDocument.GetStylesAsync(cts.Token));
    }

    [Fact]
    public async Task GetPlaceholdersAsync_ReturnsDistinctPlaceholdersInDocumentOrder()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
        try
        {
            CreateTemplateWithPlaceholders(path);
            using var templateDocument = new DocxTemplateDocument(path);

            var placeholders = await templateDocument.GetPlaceholdersAsync();

            Assert.Equal(["LICENSE", "CONTENT"], placeholders);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task GetPlaceholdersAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        using var templateDocument = new DocxTemplateDocument(templatePath);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => templateDocument.GetPlaceholdersAsync(cts.Token));
    }

    [Fact]
    public void Dispose_ReleasesUnderlyingFile()
    {
        var templateDocument = new DocxTemplateDocument(templatePath);
        templateDocument.Dispose();

        // If the file weren't released, deleting it would throw IOException.
        File.Delete(templatePath);

        Assert.False(File.Exists(templatePath));
    }

    public void Dispose()
    {
        if (File.Exists(templatePath))
            File.Delete(templatePath);
    }

    private static void CreateTemplateWithPlaceholders(string path)
    {
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);

        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body(
            new Paragraph(new Run(new Text("Intro"))),
            new Paragraph(new Run(new Text("{{LICENSE}}"))),
            new Paragraph(new Run(new Text("Not {{CONTENT}} inline"))),
            new Paragraph(new Run(new Text("{{CONTENT}}"))),
            new Paragraph(new Run(new Text("{{LICENSE}}")))));

        mainPart.Document.Save();
    }

    private static void CreateTestTemplate(string path)
    {
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);

        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body(new Paragraph(new Run(new Text("Hello")))));

        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        var normal = new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
        normal.Append(new StyleName { Val = "Normal" });
        styles.Append(normal);

        var heading1 = new Style { Type = StyleValues.Paragraph, StyleId = "Heading1" };
        heading1.Append(new StyleName { Val = "heading 1" });
        heading1.Append(new BasedOn { Val = "Normal" });
        heading1.Append(new Aliases { Val = "H1, Section Heading" });
        styles.Append(heading1);

        var defaultFont = new Style { Type = StyleValues.Character, StyleId = "DefaultParagraphFont", Default = true };
        defaultFont.Append(new StyleName { Val = "Default Paragraph Font" });
        styles.Append(defaultFont);

        var customStyle = new Style { Type = StyleValues.Paragraph, StyleId = "MyCustomStyle", CustomStyle = true };
        customStyle.Append(new StyleName { Val = "My Custom Style" });
        styles.Append(customStyle);

        stylesPart.Styles = styles;

        stylesPart.Styles.Save();
        mainPart.Document.Save();
    }
}
